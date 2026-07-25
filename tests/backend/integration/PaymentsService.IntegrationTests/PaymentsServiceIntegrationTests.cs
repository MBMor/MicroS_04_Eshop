using System.Net;
using System.Net.Http.Json;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentsService.Contracts;
using PaymentsService.Data;
using PaymentsService.Domain;
using PaymentsService.IntegrationTests.Infrastructure;
using Xunit;

namespace PaymentsService.IntegrationTests;

public sealed class PaymentsServiceIntegrationTests(
    PaymentsServiceFixture fixture)
    : IClassFixture<PaymentsServiceFixture>,
      IAsyncLifetime
{
    private const string PaymentsPath =
        "/api/v1/payments";

    public ValueTask InitializeAsync()
    {
        return fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task
        HealthAnonymousRequestReturnsOk()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync("/health", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task
        PaymentsAnonymousRequestReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync(PaymentsPath, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task
        PaymentsCustomerUserReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                PaymentsPath,
                CreateSubject("customer"),
                EshopRoles.Customer);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData(EshopRoles.Support)]
    [InlineData(EshopRoles.Admin)]
    public async Task
        GetPaymentsOperationalRoleReturnsOk(
            string role)
    {
        using HttpResponseMessage response =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                PaymentsPath,
                role);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        PaymentResponse[]? payments =
            await response.Content
                .ReadFromJsonAsync<PaymentResponse[]>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(payments);
        Assert.Empty(payments);
    }

    [Fact]
    public async Task
        CreatePaymentSuccessMethodPersistsAuthorizedPayment()
    {
        Guid orderId =
            Guid.NewGuid();

        CreatePaymentRequest requestBody = new()
        {
            OrderId = orderId,
            CustomerId = "  customer-1  ",
            Amount = 2_500m,
            Currency = "czk",
            PaymentMethod = "  TEST-SUCCESS  "
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                PaymentsPath,
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        PaymentResponse? created =
            await response.Content
                .ReadFromJsonAsync<PaymentResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(orderId, created.OrderId);
        Assert.Equal("customer-1", created.CustomerId);
        Assert.Equal(2_500m, created.Amount);
        Assert.Equal("CZK", created.Currency);
        Assert.Equal("test-success", created.PaymentMethod);
        Assert.Equal("Authorized", created.Status);
        Assert.Null(created.FailureReason);
        Assert.NotNull(created.ProcessedAtUtc);

        Assert.NotNull(response.Headers.Location);

        Assert.EndsWith(
            $"/api/v1/payments/{created.Id}",
            response.Headers.Location.ToString(),
            StringComparison.Ordinal);

        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        PaymentsDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<PaymentsDbContext>();

        Payment persisted =
            await dbContext.Payments
                .AsNoTracking()
                .SingleAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(orderId, persisted.OrderId);
        Assert.Equal("customer-1", persisted.CustomerId);
        Assert.Equal(2_500m, persisted.Amount);
        Assert.Equal("CZK", persisted.Currency);

        Assert.Equal(
            PaymentStatus.Authorized,
            persisted.Status);

        Assert.Null(persisted.FailureReason);
        Assert.NotNull(persisted.ProcessedAtUtc);
    }

    [Fact]
    public async Task
        CreatePaymentFailureMethodPersistsFailedPayment()
    {
        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                PaymentsPath,
                CreateRequest(
                    paymentMethod: "test-fail"),
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        PaymentResponse? created =
            await response.Content
                .ReadFromJsonAsync<PaymentResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(created);
        Assert.Equal("Failed", created.Status);

        Assert.Equal(
            "Simulated payment failure.",
            created.FailureReason);

        Assert.NotNull(created.ProcessedAtUtc);

        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        PaymentsDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<PaymentsDbContext>();

        Payment persisted =
            await dbContext.Payments
                .AsNoTracking()
                .SingleAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            PaymentStatus.Failed,
            persisted.Status);

        Assert.Equal(
            "Simulated payment failure.",
            persisted.FailureReason);

        Assert.NotNull(persisted.ProcessedAtUtc);
    }

    [Fact]
    public async Task
        CreatePaymentDuplicateOrderReturnsConflict()
    {
        Guid orderId =
            Guid.NewGuid();

        CreatePaymentRequest requestBody =
            CreateRequest(orderId: orderId);

        using HttpResponseMessage firstResponse =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                PaymentsPath,
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        using HttpResponseMessage duplicateResponse =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                PaymentsPath,
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);

        ProblemDetails? problem =
            await duplicateResponse.Content
                .ReadFromJsonAsync<ProblemDetails>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal("Payment conflict.", problem.Title);

        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        PaymentsDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<PaymentsDbContext>();

        Assert.Equal(
            1,
            await dbContext.Payments.CountAsync(Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task
        CreatePaymentUnsupportedMethodReturnsBadRequestWithoutPersistence()
    {
        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                PaymentsPath,
                CreateRequest(
                    paymentMethod: "credit-card"),
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(
            "Payment validation failed.",
            problem.Title);

        Assert.Contains(
            "Unsupported fake payment method",
            problem.Detail,
            StringComparison.Ordinal);

        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task
        CreatePaymentEmptyOrderIdReturnsValidationProblem()
    {
        CreatePaymentRequest requestBody =
            CreateRequest(orderId: Guid.Empty);

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                PaymentsPath,
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ValidationProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ValidationProblemDetails>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        Assert.Contains(
            nameof(CreatePaymentRequest.OrderId),
            problem.Errors.Keys);

        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task
        GetPaymentsReturnsPersistedPayments()
    {
        PaymentResponse firstPayment =
            await CreatePaymentAsync(
                paymentMethod: "test-success");

        PaymentResponse secondPayment =
            await CreatePaymentAsync(
                paymentMethod: "test-fail");

        using HttpResponseMessage response =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                PaymentsPath,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        PaymentResponse[]? payments =
            await response.Content
                .ReadFromJsonAsync<PaymentResponse[]>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(payments);
        Assert.Equal(2, payments.Length);

        Assert.Contains(
            payments,
            payment => payment.Id == firstPayment.Id);

        Assert.Contains(
            payments,
            payment => payment.Id == secondPayment.Id);
    }

    [Fact]
    public async Task
        GetPaymentByIdAndOrderReturnsPersistedPayment()
    {
        PaymentResponse created =
            await CreatePaymentAsync();

        using HttpResponseMessage byIdResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{PaymentsPath}/{created.Id}",
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.OK,
            byIdResponse.StatusCode);

        PaymentResponse? byId =
            await byIdResponse.Content
                .ReadFromJsonAsync<PaymentResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(byId);
        AssertPaymentBusinessValuesEqual(
            created,
            byId);

        using HttpResponseMessage byOrderResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{PaymentsPath}/by-order/{created.OrderId}",
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.OK,
            byOrderResponse.StatusCode);

        PaymentResponse? byOrder =
            await byOrderResponse.Content
                .ReadFromJsonAsync<PaymentResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(byOrder);
        AssertPaymentBusinessValuesEqual(
            created,
            byOrder);
    }

    [Fact]
    public async Task
        MissingPaymentQueriesReturnNotFound()
    {
        using HttpResponseMessage byIdResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{PaymentsPath}/{Guid.NewGuid()}",
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.NotFound,
            byIdResponse.StatusCode);

        using HttpResponseMessage byOrderResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{PaymentsPath}/by-order/{Guid.NewGuid()}",
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.NotFound,
            byOrderResponse.StatusCode);
    }

    private async Task<PaymentResponse>
        CreatePaymentAsync(
            string paymentMethod = "test-success")
    {
        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                PaymentsPath,
                CreateRequest(
                    paymentMethod: paymentMethod),
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        PaymentResponse? payment =
            await response.Content
                .ReadFromJsonAsync<PaymentResponse>(Xunit.TestContext.Current.CancellationToken);

        return Assert.IsType<PaymentResponse>(
            payment);
    }

    private async Task AssertDatabaseIsEmptyAsync()
    {
        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        PaymentsDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<PaymentsDbContext>();

        Assert.Equal(
            0,
            await dbContext.Payments.CountAsync(Xunit.TestContext.Current.CancellationToken));
    }

    private async Task<HttpResponseMessage>
        SendAuthenticatedAsync(
            HttpMethod method,
            string path,
            string role)
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                method,
                path,
                CreateSubject(role),
                role);

        return await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage>
        SendAuthenticatedJsonAsync<TBody>(
            HttpMethod method,
            string path,
            TBody body,
            string role)
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                method,
                path,
                CreateSubject(role),
                role);

        request.Content =
            JsonContent.Create(body);

        return await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);
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

    private static CreatePaymentRequest CreateRequest(
        Guid? orderId = null,
        string paymentMethod = "test-success")
    {
        return new CreatePaymentRequest
        {
            OrderId = orderId ?? Guid.NewGuid(),
            CustomerId =
                $"customer-{Guid.NewGuid():N}",

            Amount = 1_500m,
            Currency = "CZK",
            PaymentMethod = paymentMethod
        };
    }

    private static void AssertPaymentBusinessValuesEqual(
        PaymentResponse expected,
        PaymentResponse actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.OrderId, actual.OrderId);
        Assert.Equal(expected.CustomerId, actual.CustomerId);
        Assert.Equal(expected.Amount, actual.Amount);
        Assert.Equal(expected.Currency, actual.Currency);

        Assert.Equal(
            expected.PaymentMethod,
            actual.PaymentMethod);

        Assert.Equal(expected.Status, actual.Status);

        Assert.Equal(
            expected.FailureReason,
            actual.FailureReason);

        Assert.NotEqual(
            default,
            actual.CreatedAtUtc);

        Assert.NotNull(actual.ProcessedAtUtc);
    }

    private static string CreateSubject(
        string scenario)
    {
        return $"{scenario}-{Guid.NewGuid():N}";
    }
}
