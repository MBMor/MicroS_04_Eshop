using System.Net;
using System.Net.Http.Json;
using ApiGateway.IntegrationTests.Infrastructure;
using Eshop.Security.Authorization;
using Xunit;

namespace ApiGateway.IntegrationTests;

public sealed class GatewayAuthorizationTests(
    ApiGatewayFixture fixture)
    : IClassFixture<ApiGatewayFixture>
{
    private readonly HttpClient _client =
        fixture.Client;

    [Fact]
    public async Task Root_Anonymous_ReturnsOk()
    {
        using HttpResponseMessage response =
            await _client.GetAsync("/");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Catalog_Anonymous_ForwardsRequest()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                "/api/v1/products");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        ForwardedResponse? forwardedResponse =
            await response.Content
                .ReadFromJsonAsync<ForwardedResponse>();

        Assert.NotNull(forwardedResponse);

        Assert.Equal(
            HttpMethod.Get.Method,
            forwardedResponse.Method);

        Assert.Equal(
            "/api/v1/products",
            forwardedResponse.Path);
    }

    [Fact]
    public async Task AuthMe_Anonymous_ReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                "/api/v1/auth/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_Authenticated_ReturnsClaims()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/auth/me",
                subject: "alice.customer",
                EshopRoles.Customer);

        using HttpResponseMessage response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        AuthenticatedUserResponse? user =
            await response.Content
                .ReadFromJsonAsync<
                    AuthenticatedUserResponse>();

        Assert.NotNull(user);

        Assert.Equal(
            "alice.customer",
            user.Subject);

        Assert.Equal(
            "alice.customer",
            user.PreferredUsername);

        Assert.Equal(
            "alice.customer@eshop.local",
            user.Email);

        Assert.Equal(
            [EshopRoles.Customer],
            user.Roles);
    }

    [Fact]
    public async Task Basket_Anonymous_ReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                "/api/v1/basket");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Basket_Customer_ForwardsRequest()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                subject: "alice.customer",
                EshopRoles.Customer);

        await AssertRequestWasForwardedAsync(
            request,
            "/api/v1/basket");
    }

    [Fact]
    public async Task Orders_Customer_ForwardsRequest()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/orders",
                subject: "alice.customer",
                EshopRoles.Customer);

        await AssertRequestWasForwardedAsync(
            request,
            "/api/v1/orders");
    }

    [Fact]
    public async Task Notifications_Customer_ForwardsRequest()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/notifications",
                subject: "alice.customer",
                EshopRoles.Customer);

        await AssertRequestWasForwardedAsync(
            request,
            "/api/v1/notifications");
    }

    [Fact]
    public async Task Basket_Support_ReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                subject: "sam.support",
                EshopRoles.Support);

        using HttpResponseMessage response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Orders_AdminWithoutCustomerRole_ReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/orders",
                subject: "anna.admin",
                EshopRoles.Admin);

        using HttpResponseMessage response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Inventory_Customer_ReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/inventory-items",
                subject: "alice.customer",
                EshopRoles.Customer);

        using HttpResponseMessage response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Inventory_Support_ForwardsRequest()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/inventory-items",
                subject: "sam.support",
                EshopRoles.Support);

        await AssertRequestWasForwardedAsync(
            request,
            "/api/v1/inventory-items");
    }

    [Fact]
    public async Task Inventory_Admin_ForwardsRequest()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/inventory-items",
                subject: "anna.admin",
                EshopRoles.Admin);

        await AssertRequestWasForwardedAsync(
            request,
            "/api/v1/inventory-items");
    }

    [Fact]
    public async Task Payments_Support_ForwardsRequest()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/payments",
                subject: "sam.support",
                EshopRoles.Support);

        await AssertRequestWasForwardedAsync(
            request,
            "/api/v1/payments");
    }

    [Fact]
    public async Task Payments_Admin_ForwardsRequest()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/payments",
                subject: "anna.admin",
                EshopRoles.Admin);

        await AssertRequestWasForwardedAsync(
            request,
            "/api/v1/payments");
    }

    private async Task AssertRequestWasForwardedAsync(
        HttpRequestMessage request,
        string expectedPath)
    {
        using HttpResponseMessage response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        ForwardedResponse? forwardedResponse =
            await response.Content
                .ReadFromJsonAsync<ForwardedResponse>();

        Assert.NotNull(forwardedResponse);

        Assert.Equal(
            expectedPath,
            forwardedResponse.Path);
    }

    private static HttpRequestMessage
        CreateAuthenticatedRequest(
            HttpMethod method,
            string requestUri,
            string subject,
            params string[] roles)
    {
        HttpRequestMessage request =
            new(method, requestUri);

        request.Headers.Add(
            TestAuthenticationHandler
                .SubjectHeaderName,
            subject);

        if (roles.Length > 0)
        {
            request.Headers.Add(
                TestAuthenticationHandler
                    .RolesHeaderName,
                string.Join(',', roles));
        }

        return request;
    }

    private sealed record
        AuthenticatedUserResponse(
            string? Subject,
            string? PreferredUsername,
            string? Email,
            string[] Roles);
}
