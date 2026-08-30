using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Eshop.Messaging.RabbitMq;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Http;
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

    private const string OperationalOrdersPath =
        "/api/v1/operations/orders";

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
        HealthAnonymousRequestReturnsOk()
    {
        foreach (string endpoint in new[] { "/live", "/ready", "/health" })
        {
            using HttpResponseMessage response =
                await fixture.Client.GetAsync(
                    endpoint,
                    Xunit.TestContext.Current.CancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task
        OrdersAnonymousRequestReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync(OrdersPath, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task
        OrdersSupportUserReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                OrdersPath,
                CreateSubject("support"),
                EshopRoles.Support);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task
        OperationalOrdersSupportUserCanInspectOrdersAcrossCustomers()
    {
        string alice =
            CreateSubject("operations-alice");

        string bob =
            CreateSubject("operations-bob");

        OrderResponse aliceOrder =
            await CreateOrderAsync(
                alice,
                "alice@example.com");

        OrderResponse bobOrder =
            await CreateOrderAsync(
                bob,
                "bob@example.com");

        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                OperationalOrdersPath,
                CreateSubject("support"),
                EshopRoles.Support);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(
                request,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        OperationalOrderPageResponse? page =
            await response.Content
                .ReadFromJsonAsync<OperationalOrderPageResponse>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(page);

        Assert.Contains(
            page.Items,
            order =>
                order.Id == aliceOrder.Id
                && order.CustomerEmail
                    == "alice@example.com");

        Assert.Contains(
            page.Items,
            order =>
                order.Id == bobOrder.Id
                && order.CustomerEmail
                    == "bob@example.com");
    }

    [Fact]
    public async Task
        OperationalOrdersCustomerUserReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                OperationalOrdersPath,
                CreateSubject("customer"),
                EshopRoles.Customer);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(
                request,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task
        OperationalOrdersReturnsBoundedPages()
    {
        await CreateOrderAsync(
            CreateSubject("page-one"),
            "one@example.com");

        await CreateOrderAsync(
            CreateSubject("page-two"),
            "two@example.com");

        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"{OperationalOrdersPath}?offset=0&limit=1",
                CreateSubject("support"),
                EshopRoles.Support);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(
                request,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        OperationalOrderPageResponse? page =
            await response.Content
                .ReadFromJsonAsync<OperationalOrderPageResponse>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(0, page.Offset);
        Assert.Equal(1, page.Limit);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task
        OperationalOrderDetailReturnsCustomerAndWorkflowDetails()
    {
        string customerId =
            CreateSubject("detail-customer");

        OrderResponse created =
            await CreateOrderAsync(
                customerId,
                "detail@example.com");

        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"{OperationalOrdersPath}/{created.Id}",
                CreateSubject("admin"),
                EshopRoles.Admin);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(
                request,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        OperationalOrderResponse? order =
            await response.Content
                .ReadFromJsonAsync<OperationalOrderResponse>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(order);
        Assert.Equal(created.Id, order.Id);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal("detail@example.com", order.CustomerEmail);
        Assert.NotEmpty(order.Items);
        Assert.NotEmpty(order.StatusHistory);
    }

    [Fact]
    public async Task
        OperationalOrdersInvalidLimitReturnsBadRequest()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"{OperationalOrdersPath}?limit=101",
                CreateSubject("support"),
                EshopRoles.Support);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(
                request,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task
        CreateOrderValidBasketPersistsOrderHistoryAndOutbox()
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

        request.Headers.Add(
            OrderHeaders.IdempotencyKey,
            Guid.NewGuid().ToString());

        request.Content =
            JsonContent.Create(requestBody);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        OrderResponse? order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>(Xunit.TestContext.Current.CancellationToken);

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
                .SingleAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(order.Id, persistedOrder.Id);
        Assert.Equal(subject, persistedOrder.CustomerId);
        Assert.Equal(2, persistedOrder.Items.Count);
        Assert.Single(persistedOrder.StatusHistory);

        Outbox.OutboxMessage outboxMessage =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            RabbitMqRoutingKeys.OrderCreatedV1,
            outboxMessage.RoutingKey);

        Assert.Contains(
            order.Id.ToString(),
            outboxMessage.Payload,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid key")]
    public async Task
        CreateOrderMissingOrMalformedIdempotencyKeyReturnsBadRequestWithoutPersistence(
            string? idempotencyKey)
    {
        string subject =
            CreateSubject("invalid-idempotency-key");

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem());

        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Post,
                OrdersPath,
                subject);

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation(
                OrderHeaders.IdempotencyKey,
                idempotencyKey);
        }

        request.Content = JsonContent.Create(
            new CreateOrderRequest
            {
                CustomerEmail = "alice@example.com",
                PaymentMethod = "test-success"
            });

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(
                request,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            0,
            fixture.BasketClient
                .GetBasketCallCount(subject));

        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task
        CreateOrderSameKeyReplaysStoredOrderWithoutReloadingChangedBasket()
    {
        string subject =
            CreateSubject("sequential-replay");

        string idempotencyKey =
            Guid.NewGuid().ToString();

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem(
                productName: "Original item",
                unitPrice: 100m));

        using HttpResponseMessage firstResponse =
            await SendCreateOrderAsync(
                subject,
                "alice@example.com",
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        Assert.NotNull(firstResponse.Headers.Location);

        string firstLocation =
            firstResponse.Headers.Location.ToString();

        OrderResponse firstOrder =
            await ReadOrderAsync(firstResponse);

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem(
                productName: "Changed item",
                unitPrice: 999m));

        using HttpResponseMessage replayResponse =
            await SendCreateOrderAsync(
                subject,
                "alice@example.com",
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.OK,
            replayResponse.StatusCode);

        Assert.Equal(
            "true",
            Assert.Single(
                replayResponse.Headers.GetValues(
                    OrderHeaders.IdempotentReplayed)));

        Assert.NotNull(replayResponse.Headers.Location);

        Assert.Equal(
            firstLocation,
            replayResponse.Headers.Location.ToString());

        OrderResponse replayedOrder =
            await ReadOrderAsync(replayResponse);

        Assert.Equal(firstOrder.Id, replayedOrder.Id);
        Assert.Equal(firstOrder.TotalAmount, replayedOrder.TotalAmount);
        Assert.Equal(firstOrder.Items.Length, replayedOrder.Items.Length);

        Assert.Equal(
            1,
            fixture.BasketClient
                .GetBasketCallCount(subject));

        Assert.Equal(
            1,
            fixture.BasketClient
                .GetClearCallCount(subject));

        await AssertDatabaseCountsAsync(
            expectedOrders: 1,
            expectedOutboxMessages: 1,
            expectedIdempotencyRecords: 1);
    }

    [Fact]
    public async Task
        CreateOrderSameKeyWithChangedRequestReturnsConflictWithoutSideEffects()
    {
        string subject =
            CreateSubject("changed-request");

        string idempotencyKey =
            Guid.NewGuid().ToString();

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem());

        using HttpResponseMessage firstResponse =
            await SendCreateOrderAsync(
                subject,
                "alice@example.com",
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        using HttpResponseMessage conflictResponse =
            await SendCreateOrderAsync(
                subject,
                "changed@example.com",
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.Conflict,
            conflictResponse.StatusCode);

        ProblemDetails? problem =
            await conflictResponse.Content
                .ReadFromJsonAsync<ProblemDetails>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(
            "urn:eshop:problem:idempotency-key-reused",
            problem.Type);

        Assert.Equal(
            1,
            fixture.BasketClient
                .GetBasketCallCount(subject));

        Assert.Equal(
            1,
            fixture.BasketClient
                .GetClearCallCount(subject));

        await AssertDatabaseCountsAsync(
            expectedOrders: 1,
            expectedOutboxMessages: 1,
            expectedIdempotencyRecords: 1);
    }

    [Fact]
    public async Task
        ConcurrentIdenticalCreateOrderRequestsCreateOneOrderAndOutbox()
    {
        string subject =
            CreateSubject("concurrent-replay");

        string idempotencyKey =
            Guid.NewGuid().ToString();

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem());

        fixture.BasketClient.SynchronizeNextBasketReads(
            subject,
            participantCount: 2);

        Task<HttpResponseMessage> firstRequest =
            SendCreateOrderAsync(
                subject,
                "alice@example.com",
                idempotencyKey);

        Task<HttpResponseMessage> secondRequest =
            SendCreateOrderAsync(
                subject,
                "alice@example.com",
                idempotencyKey);

        HttpResponseMessage[] responses =
            await Task.WhenAll(
                firstRequest,
                secondRequest);

        try
        {
            Assert.Equal(
                [HttpStatusCode.OK, HttpStatusCode.Created],
                responses
                    .Select(response => response.StatusCode)
                    .Order()
                    .ToArray());

            OrderResponse[] orders =
                await Task.WhenAll(
                    responses.Select(ReadOrderAsync));

            Assert.Equal(orders[0].Id, orders[1].Id);

            HttpResponseMessage replayResponse =
                Assert.Single(
                    responses,
                    response =>
                        response.StatusCode
                            == HttpStatusCode.OK);

            Assert.Equal(
                "true",
                Assert.Single(
                    replayResponse.Headers.GetValues(
                        OrderHeaders.IdempotentReplayed)));
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        Assert.Equal(
            1,
            fixture.BasketClient
                .GetClearCallCount(subject));

        Assert.Equal(
            2,
            fixture.BasketClient
                .GetBasketCallCount(subject));

        await AssertDatabaseCountsAsync(
            expectedOrders: 1,
            expectedOutboxMessages: 1,
            expectedIdempotencyRecords: 1);
    }

    [Fact]
    public async Task
        SameIdempotencyKeyIsScopedToAuthenticatedCustomer()
    {
        string firstSubject =
            CreateSubject("scope-first");

        string secondSubject =
            CreateSubject("scope-second");

        string idempotencyKey =
            Guid.NewGuid().ToString();

        fixture.BasketClient.SetBasket(
            firstSubject,
            CreateBasketItem(productName: "First"));

        fixture.BasketClient.SetBasket(
            secondSubject,
            CreateBasketItem(productName: "Second"));

        using HttpResponseMessage firstResponse =
            await SendCreateOrderAsync(
                firstSubject,
                "first@example.com",
                idempotencyKey);

        using HttpResponseMessage secondResponse =
            await SendCreateOrderAsync(
                secondSubject,
                "second@example.com",
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            secondResponse.StatusCode);

        OrderResponse firstOrder =
            await ReadOrderAsync(firstResponse);

        OrderResponse secondOrder =
            await ReadOrderAsync(secondResponse);

        Assert.NotEqual(firstOrder.Id, secondOrder.Id);

        await AssertDatabaseCountsAsync(
            expectedOrders: 2,
            expectedOutboxMessages: 2,
            expectedIdempotencyRecords: 2);
    }

    [Fact]
    public async Task
        NewIdempotencyKeyUsesCurrentBasket()
    {
        string subject =
            CreateSubject("new-intent");

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem(
                productName: "First",
                unitPrice: 100m));

        using HttpResponseMessage firstResponse =
            await SendCreateOrderAsync(
                subject,
                "alice@example.com",
                Guid.NewGuid().ToString());

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        OrderResponse firstOrder =
            await ReadOrderAsync(firstResponse);

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem(
                productName: "Second",
                unitPrice: 250m,
                quantity: 2));

        using HttpResponseMessage secondResponse =
            await SendCreateOrderAsync(
                subject,
                "alice@example.com",
                Guid.NewGuid().ToString());

        Assert.Equal(
            HttpStatusCode.Created,
            secondResponse.StatusCode);

        OrderResponse secondOrder =
            await ReadOrderAsync(secondResponse);

        Assert.NotEqual(firstOrder.Id, secondOrder.Id);
        Assert.Equal(100m, firstOrder.TotalAmount);
        Assert.Equal(500m, secondOrder.TotalAmount);

        await AssertDatabaseCountsAsync(
            expectedOrders: 2,
            expectedOutboxMessages: 2,
            expectedIdempotencyRecords: 2);
    }

    [Fact]
    public async Task
        CommittedOrderReplaysWhenBasketClearFails()
    {
        string subject =
            CreateSubject("clear-failure-replay");

        string idempotencyKey =
            Guid.NewGuid().ToString();

        fixture.BasketClient.SetBasket(
            subject,
            CreateBasketItem());

        fixture.BasketClient.FailBasketClear();

        using HttpResponseMessage firstResponse =
            await SendCreateOrderAsync(
                subject,
                "alice@example.com",
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        OrderResponse firstOrder =
            await ReadOrderAsync(firstResponse);

        using HttpResponseMessage replayResponse =
            await SendCreateOrderAsync(
                subject,
                "alice@example.com",
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.OK,
            replayResponse.StatusCode);

        OrderResponse replayedOrder =
            await ReadOrderAsync(replayResponse);

        Assert.Equal(firstOrder.Id, replayedOrder.Id);

        Assert.Equal(
            1,
            fixture.BasketClient
                .GetBasketCallCount(subject));

        Assert.Equal(
            1,
            fixture.BasketClient
                .GetClearCallCount(subject));

        await AssertDatabaseCountsAsync(
            expectedOrders: 1,
            expectedOutboxMessages: 1,
            expectedIdempotencyRecords: 1);
    }

    [Fact]
    public async Task
        CreateOrderEmptyBasketReturnsBadRequestWithoutPersistence()
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
                .ReadFromJsonAsync<ProblemDetails>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        AssertProblemContract(
            problem,
            expectedTitle: "Checkout failed.",
            expectedDetail: "The basket is empty.");

        Assert.Equal(
            0,
            fixture.BasketClient
                .GetClearCallCount(subject));

        BasketSnapshot retainedBasket =
            await fixture.BasketClient.GetBasketAsync(
                subject,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(retainedBasket.Items);

        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task
        CreateOrderMultipleCurrenciesReturnsBadRequest()
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

        ProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        AssertProblemContract(
            problem,
            expectedTitle: "Checkout failed.",
            expectedDetail:
                "An order cannot contain items in multiple currencies.");

        Assert.Equal(
            0,
            fixture.BasketClient
                .GetClearCallCount(subject));

        BasketSnapshot retainedBasket =
            await fixture.BasketClient.GetBasketAsync(
                subject,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            ["CZK", "EUR"],
            retainedBasket.Items
                .Select(item => item.Currency)
                .ToArray());

        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task
        GetOrdersReturnsOnlyAuthenticatedCustomersOrders()
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
            await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        OrderSummaryResponse[]? orders =
            await response.Content
                .ReadFromJsonAsync<OrderSummaryResponse[]>(Xunit.TestContext.Current.CancellationToken);

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
        GetOrderOtherCustomersOrderReturnsNotFound()
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
            await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task
        CreateOrderInvalidEmailReturnsBadRequest()
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

        request.Headers.Add(
            OrderHeaders.IdempotencyKey,
            Guid.NewGuid().ToString());

        request.Content = JsonContent.Create(
            new
            {
                customerEmail = "not-an-email",
                paymentMethod = "test-success"
            });

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ValidationProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ValidationProblemDetails>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        AssertProblemContract(
            problem,
            expectedTitle: "Request validation failed.",
            expectedDetail:
                "One or more validation errors occurred.");

        Assert.Contains(
            "CustomerEmail",
            problem.Errors.Keys);

        Assert.Equal(
            "model_validation_failed",
            GetRequiredStringExtension(
                problem,
                "errorCode"));

        Assert.Equal(
            0,
            fixture.BasketClient
                .GetBasketCallCount(subject));

        Assert.Equal(
            0,
            fixture.BasketClient
                .GetClearCallCount(subject));

        BasketSnapshot retainedBasket =
            await fixture.BasketClient.GetBasketAsync(
                subject,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Single(retainedBasket.Items);

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
                .ReadFromJsonAsync<OrderResponse>(Xunit.TestContext.Current.CancellationToken);

        return Assert.IsType<OrderResponse>(order);
    }

    private async Task<HttpResponseMessage>
        SendCreateOrderAsync(
            string subject,
            string email,
            string? idempotencyKey = null,
            string paymentMethod = "test-success")
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
                PaymentMethod = paymentMethod
            });

        request.Headers.Add(
            OrderHeaders.IdempotencyKey,
            idempotencyKey ?? Guid.NewGuid().ToString());

        return await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);
    }

    private static async Task<OrderResponse> ReadOrderAsync(
        HttpResponseMessage response)
    {
        OrderResponse? order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>(
                    Xunit.TestContext.Current.CancellationToken);

        return Assert.IsType<OrderResponse>(order);
    }

    private async Task AssertDatabaseIsEmptyAsync()
    {
        await AssertDatabaseCountsAsync(
            expectedOrders: 0,
            expectedOutboxMessages: 0,
            expectedIdempotencyRecords: 0);

        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        OrdersDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        Assert.Equal(
            0,
            await dbContext.OrderItems.CountAsync(
                Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(
            0,
            await dbContext.OrderStatusHistories.CountAsync(
                Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(
            0,
            await dbContext.ProcessedMessages.CountAsync(
                Xunit.TestContext.Current.CancellationToken));
    }

    private static void AssertProblemContract(
        ProblemDetails problem,
        string expectedTitle,
        string expectedDetail)
    {
        Assert.Equal(
            StatusCodes.Status400BadRequest,
            problem.Status);

        Assert.Equal(
            "https://httpstatuses.com/400",
            problem.Type);

        Assert.Equal(
            expectedTitle,
            problem.Title);

        Assert.Equal(
            expectedDetail,
            problem.Detail);

        Assert.Equal(
            OrdersPath,
            problem.Instance);

        Assert.False(
            string.IsNullOrWhiteSpace(
                GetRequiredStringExtension(
                    problem,
                    "traceId")));

        Assert.False(
            string.IsNullOrWhiteSpace(
                GetRequiredStringExtension(
                    problem,
                    "requestId")));
    }

    private static string GetRequiredStringExtension(
        ProblemDetails problem,
        string key)
    {
        JsonElement value = Assert.IsType<JsonElement>(
            problem.Extensions[key]);

        return Assert.IsType<string>(
            value.GetString());
    }

    private async Task AssertDatabaseCountsAsync(
        int expectedOrders,
        int expectedOutboxMessages,
        int expectedIdempotencyRecords)
    {
        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        OrdersDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        Assert.Equal(
            expectedOrders,
            await dbContext.Orders.CountAsync(Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(
            expectedOutboxMessages,
            await dbContext.OutboxMessages.CountAsync(Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(
            expectedIdempotencyRecords,
            await dbContext.OrderIdempotencyRecords
                .CountAsync(Xunit.TestContext.Current.CancellationToken));
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
