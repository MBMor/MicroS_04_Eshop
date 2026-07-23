using System.Net;
using System.Text.Json;
using ApiGateway.IntegrationTests.Infrastructure;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ApiGateway.IntegrationTests;

public sealed class GatewayRateLimitingTests
{
    [Fact]
    public async Task
        Catalog_AnonymousClientExceedsLimit_ReturnsTooManyRequests()
    {
        await using GatewayRateLimitingTestHost host =
            await GatewayRateLimitingTestHost.StartAsync();

        using HttpResponseMessage firstResponse =
            await host.Client.GetAsync(
                "/api/v1/products");

        using HttpResponseMessage secondResponse =
            await host.Client.GetAsync(
                "/api/v1/products");

        using HttpResponseMessage rejectedResponse =
            await host.Client.GetAsync(
                "/api/v1/products");

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode);

        await AssertRateLimitedAsync(
            rejectedResponse);
    }

    [Fact]
    public async Task
        Basket_SameCustomerExceedsLimit_ReturnsTooManyRequests()
    {
        await using GatewayRateLimitingTestHost host =
            await GatewayRateLimitingTestHost.StartAsync();

        using HttpResponseMessage firstResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Get,
                "/api/v1/basket",
                "basket-customer",
                EshopRoles.Customer);

        using HttpResponseMessage secondResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Get,
                "/api/v1/basket",
                "basket-customer",
                EshopRoles.Customer);

        using HttpResponseMessage rejectedResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Get,
                "/api/v1/basket",
                "basket-customer",
                EshopRoles.Customer);

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode);

        await AssertRateLimitedAsync(
            rejectedResponse);
    }

    [Fact]
    public async Task
        Checkout_SameCustomerExceedsLimit_ReturnsTooManyRequests()
    {
        await using GatewayRateLimitingTestHost host =
            await GatewayRateLimitingTestHost.StartAsync();

        using HttpResponseMessage firstResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Post,
                "/api/v1/orders",
                "checkout-customer",
                EshopRoles.Customer);

        using HttpResponseMessage rejectedResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Post,
                "/api/v1/orders",
                "checkout-customer",
                EshopRoles.Customer);

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode);

        await AssertRateLimitedAsync(
            rejectedResponse);
    }

    [Fact]
    public async Task
        Checkout_DifferentCustomersHaveIndependentLimits()
    {
        await using GatewayRateLimitingTestHost host =
            await GatewayRateLimitingTestHost.StartAsync();

        using HttpResponseMessage aliceResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Post,
                "/api/v1/orders",
                "alice-customer",
                EshopRoles.Customer);

        using HttpResponseMessage bobResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Post,
                "/api/v1/orders",
                "bob-customer",
                EshopRoles.Customer);

        Assert.Equal(
            HttpStatusCode.OK,
            aliceResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            bobResponse.StatusCode);
    }

    [Fact]
    public async Task
        OperationalEndpoint_SameUserExceedsLimit_ReturnsTooManyRequests()
    {
        await using GatewayRateLimitingTestHost host =
            await GatewayRateLimitingTestHost.StartAsync();

        using HttpResponseMessage firstResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Get,
                "/api/v1/inventory-items",
                "support-user",
                EshopRoles.Support);

        using HttpResponseMessage rejectedResponse =
            await SendAuthenticatedAsync(
                host.Client,
                HttpMethod.Get,
                "/api/v1/inventory-items",
                "support-user",
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode);

        await AssertRateLimitedAsync(
            rejectedResponse);
    }

    [Fact]
    public async Task
        HealthEndpoint_IsNotRateLimited()
    {
        await using GatewayRateLimitingTestHost host =
            await GatewayRateLimitingTestHost.StartAsync();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage response =
                await host.Client.GetAsync("/health");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task
        CrossOriginPreflight_DoesNotGrantCorsAccess()
    {
        await using GatewayRateLimitingTestHost host =
            await GatewayRateLimitingTestHost.StartAsync();

        using HttpRequestMessage request = new(
            HttpMethod.Options,
            "/api/v1/products");

        request.Headers.TryAddWithoutValidation(
            "Origin",
            "https://untrusted.example");

        request.Headers.TryAddWithoutValidation(
            "Access-Control-Request-Method",
            "GET");

        using HttpResponseMessage response =
            await host.Client.SendAsync(request);

        Assert.False(
            response.Headers.Contains(
                "Access-Control-Allow-Origin"));
    }

    private static async Task<HttpResponseMessage>
        SendAuthenticatedAsync(
            HttpClient client,
            HttpMethod method,
            string path,
            string subject,
            params string[] roles)
    {
        using HttpRequestMessage request =
            new(method, path);

        request.Headers.Add(
            TestAuthenticationHandler.SubjectHeaderName,
            subject);

        request.Headers.Add(
            TestAuthenticationHandler.RolesHeaderName,
            string.Join(',', roles));

        return await client.SendAsync(request);
    }

    private static async Task AssertRateLimitedAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            response.StatusCode);

        Assert.NotNull(
            response.Headers.RetryAfter);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        string content =
            await response.Content.ReadAsStringAsync();

        using JsonDocument problem =
            JsonDocument.Parse(content);

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            problem.RootElement
                .GetProperty("status")
                .GetInt32());

        Assert.Equal(
            "Too Many Requests",
            problem.RootElement
                .GetProperty("title")
                .GetString());
    }
}
