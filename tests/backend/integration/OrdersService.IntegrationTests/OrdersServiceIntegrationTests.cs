using System.Net;
using System.Net.Http.Json;
using Eshop.Security.Authorization;
using Messaging.Shared.RabbitMq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Contracts;
using OrdersService.Data;
using OrdersService.Integration;
using OrdersService.IntegrationTests.Infrastructure;
using Xunit;

namespace OrdersService.IntegrationTests;

public sealed class OrdersServiceIntegrationTests(
    OrdersServiceFixture fixture)
    : IClassFixture<OrdersServiceFixture>,
      IAsyncLifetime
{
    private const string OrdersPath =
        "/api/v1/orders";

    public ValueTask InitializeAsync()
    {
        return fixture.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task
        Health_AnonymousRequest_ReturnsOk()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync("/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task
        Orders_AnonymousRequest_ReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync(OrdersPath);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task
        Orders_SupportUser_ReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                OrdersPath,
                CreateSubject("support"),
                EshopRoles.Support);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task
        CreateOrder_ValidBasket_PersistsOrderHistoryAndOutbox()
    {
        string subject =
            CreateSubject("create");

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem(
                productName: "Keyboard",
                unitPrice: 2_500m,
                quantity: 1),
            CreateBasketItem(
                productName: "Mouse",
                unitPrice: 750m,
                quantity: 1));

        CreateOrderRequest requestBody = new()
        {
            CustomerEmail =
                "alice@example.com",

            PaymentMethod =
                "test-success"
        };

        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Post,
                OrdersPath,
                subject);

        request.Content =
            JsonContent.Create(requestBody);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        OrderResponse? order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>();

        Assert.NotNull(order);
        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal("alice@example.com", order.CustomerEmail);

        Assert.Equal(
            "PendingStockReservation",
            order.Status);

        Assert.Equal(3_250m, order.TotalAmount);
        Assert.Equal("CZK", order.Currency);
        Assert.Equal("test-success", order.PaymentMethod);
        Assert.Equal(2, order.Items.Length);

        OrderStatusHistoryResponse initialHistory =
            Assert.Single(order.StatusHistory);

        Assert.Null(initialHistory.FromStatus);

        Assert.Equal(
            "PendingStockReservation",
            initialHistory.ToStatus);

        Assert.NotNull(response.Headers.Location);

        Assert.EndsWith(
            $"/api/v1/orders/{order.Id}",
            response.Headers.Location.ToString(),
            StringComparison.Ordinal);

        Assert.Equal(
            1,
            fixture.BasketClient
                .GetClearCallCount(subject));

        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        OrdersDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        Domain.Order persistedOrder =
            await dbContext.Orders
                .AsNoTracking()
                .Include(candidate => candidate.Items)
                .Include(candidate => candidate.StatusHistory)
                .SingleAsync();

        Assert.Equal(order.Id, persistedOrder.Id);
        Assert.Equal(subject, persistedOrder.CustomerId);
        Assert.Equal(2, persistedOrder.Items.Count);
        Assert.Single(persistedOrder.StatusHistory);

        Outbox.OutboxMessage outboxMessage =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            RabbitMqRoutingKeys.OrderCreatedV1,
            outboxMessage.RoutingKey);

        Assert.Contains(
            order.Id.ToString(),
            outboxMessage.Payload,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task
        CreateOrder_EmptyBasket_ReturnsBadRequestWithoutPersistence()
    {
        string subject =
            CreateSubject("empty");

        using HttpResponseMessage response =
            await SendCreateOrderAsync(
                subject,
                "empty@example.com");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Checkout failed.", problem.Title);

        Assert.Equal(
            0,
            fixture.BasketClient
                .GetClearCallCount(subject));

        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task
        CreateOrder_MultipleCurrencies_ReturnsBadRequest()
    {
        string subject =
            CreateSubject("currencies");

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem(
                productName: "Keyboard",
                unitPrice: 2_500m,
                currency: "CZK"),
            CreateBasketItem(
                productName: "Mouse",
                unitPrice: 50m,
                currency: "EUR"));

        using HttpResponseMessage response =
            await SendCreateOrderAsync(
                subject,
                "currencies@example.com");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task
        GetOrders_ReturnsOnlyAuthenticatedCustomersOrders()
    {
        string alice =
            CreateSubject("alice");

        string bob =
            CreateSubject("bob");

        OrderResponse aliceOrder =
            await CreateOrderAsync(
                alice,
                "alice@example.com");

        OrderResponse bobOrder =
            await CreateOrderAsync(
                bob,
                "bob@example.com");

        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Get,
                OrdersPath,
                alice);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        OrderSummaryResponse[]? orders =
            await response.Content
                .ReadFromJsonAsync<OrderSummaryResponse[]>();

        Assert.NotNull(orders);

        OrderSummaryResponse summary =
            Assert.Single(orders);

        Assert.Equal(aliceOrder.Id, summary.Id);

        Assert.DoesNotContain(
            orders,
            order => order.Id == bobOrder.Id);
    }

    [Fact]
    public async Task
        GetOrder_OtherCustomersOrder_ReturnsNotFound()
    {
        string owner =
            CreateSubject("owner");

        string attacker =
            CreateSubject("attacker");

        OrderResponse order =
            await CreateOrderAsync(
                owner,
                "owner@example.com");

        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Get,
                $"{OrdersPath}/{order.Id}",
                attacker);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task
        CreateOrder_InvalidEmail_ReturnsBadRequest()
    {
        string subject =
            CreateSubject("invalid-email");

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem());

        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Post,
                OrdersPath,
                subject);

        request.Content = JsonContent.Create(
            new
            {
                customerEmail = "not-an-email",
                paymentMethod = "test-success"
            });

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertDatabaseIsEmptyAsync();
    }

    private async Task<OrderResponse> CreateOrderAsync(
        string subject,
        string email)
    {
        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem());

        using HttpResponseMessage response =
            await SendCreateOrderAsync(
                subject,
                email);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        OrderResponse? order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>();

        return Assert.IsType<OrderResponse>(order);
    }

    private async Task<HttpResponseMessage>
        SendCreateOrderAsync(
            string subject,
            string email)
    {
        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Post,
                OrdersPath,
                subject);

        request.Content = JsonContent.Create(
            new CreateOrderRequest
            {
                CustomerEmail = email,
                PaymentMethod = "test-success"
            });

        return await fixture.Client.SendAsync(request);
    }

    private async Task AssertDatabaseIsEmptyAsync()
    {
        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        OrdersDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        Assert.Equal(
            0,
            await dbContext.Orders.CountAsync());

        Assert.Equal(
            0,
            await dbContext.OutboxMessages.CountAsync());
    }

    private static BasketItemSnapshot CreateBasketItem(
        string productName = "Keyboard",
        decimal unitPrice = 2_500m,
        string currency = "CZK",
        int quantity = 1)
    {
        return new BasketItemSnapshot(
            Guid.NewGuid(),
            productName,
            unitPrice,
            currency,
            quantity,
            unitPrice * quantity);
    }

    private static HttpRequestMessage
        CreateCustomerRequest(
            HttpMethod method,
            string path,
            string subject)
    {
        return CreateAuthenticatedRequest(
            method,
            path,
            subject,
            EshopRoles.Customer);
    }

    private static HttpRequestMessage
        CreateAuthenticatedRequest(
            HttpMethod method,
            string path,
            string subject,
            params string[] roles)
    {
        HttpRequestMessage request =
            new(method, path);

        request.Headers.Add(
            TestAuthenticationHandler.SubjectHeaderName,
            subject);

        request.Headers.Add(
            TestAuthenticationHandler.RolesHeaderName,
            string.Join(',', roles));

        return request;
    }

    private static string CreateSubject(
        string scenario)
    {
        return $"{scenario}-{Guid.NewGuid():N}";
    }
}
