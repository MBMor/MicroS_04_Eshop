using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public static IEnumerable<object?[]>
        RouteAuthorizationCases()
    {
        GatewayRouteRegistry registry =
            LoadRouteRegistry();

        string[] knownRoles =
        [
            EshopRoles.Customer,
            EshopRoles.Support,
            EshopRoles.Admin
        ];

        foreach (GatewayRouteContract route
                 in registry.Routes)
        {
            bool forwards =
                string.Equals(
                    route.Kind,
                    "proxy",
                    StringComparison.Ordinal);

            if (route.AuthorizationPolicy is null)
            {
                yield return CreateRouteCase(
                    route,
                    subject: null,
                    role: null,
                    HttpStatusCode.OK,
                    forwards);

                continue;
            }

            yield return CreateRouteCase(
                route,
                subject: null,
                role: null,
                HttpStatusCode.Unauthorized,
                expectedForwarding: false);

            if (string.Equals(
                    route.AuthorizationPolicy,
                    "AuthenticatedUser",
                    StringComparison.Ordinal))
            {
                yield return CreateRouteCase(
                    route,
                    subject: "matrix.authenticated",
                    role: null,
                    HttpStatusCode.OK,
                    forwards);

                continue;
            }

            string deniedRole =
                knownRoles.Except(
                    route.AllowedRoles,
                    StringComparer.Ordinal)
                .First();

            yield return CreateRouteCase(
                route,
                subject: $"matrix.{deniedRole}",
                deniedRole,
                HttpStatusCode.Forbidden,
                expectedForwarding: false);

            foreach (string allowedRole
                     in route.AllowedRoles)
            {
                yield return CreateRouteCase(
                    route,
                    subject: $"matrix.{allowedRole}",
                    allowedRole,
                    HttpStatusCode.OK,
                    forwards);
            }
        }
    }

    [Theory]
    [MemberData(nameof(RouteAuthorizationCases))]
    public async Task
        EveryAddressableRouteEnforcesAuthorizationAndForwarding(
            string routeId,
            string method,
            string path,
            string? subject,
            string? role,
            HttpStatusCode expectedStatus,
            bool expectedForwarding)
    {
        fixture.ResetForwardedRequestCount();

        using HttpRequestMessage request =
            subject is null
                ? new HttpRequestMessage(
                    new HttpMethod(method),
                    path)
                : CreateAuthenticatedRequest(
                    new HttpMethod(method),
                    path,
                    subject,
                    role is null ? [] : [role]);

        using HttpResponseMessage response =
            await _client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            expectedStatus,
            response.StatusCode);

        Assert.Equal(
            expectedForwarding ? 1 : 0,
            fixture.ForwardedRequestCount);

        if (!expectedForwarding)
        {
            return;
        }

        ForwardedResponse? forwardedResponse =
            await response.Content
                .ReadFromJsonAsync<ForwardedResponse>(
                    TestContext.Current.CancellationToken);

        Assert.NotNull(forwardedResponse);

        Assert.Equal(
            method,
            forwardedResponse.Method);

        Assert.Equal(
            path,
            forwardedResponse.Path);

        Assert.False(
            string.IsNullOrWhiteSpace(routeId));
    }

    [Fact]
    public async Task RootAnonymousReturnsOk()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                "/",
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task CatalogAnonymousForwardsRequest()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                "/api/v1/products",
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        ForwardedResponse? forwardedResponse =
            await response.Content
                .ReadFromJsonAsync<ForwardedResponse>(
                    TestContext.Current.CancellationToken);

        Assert.NotNull(forwardedResponse);

        Assert.Equal(
            HttpMethod.Get.Method,
            forwardedResponse.Method);

        Assert.Equal(
            "/api/v1/products",
            forwardedResponse.Path);
    }

    [Theory]
    [InlineData("POST", "/api/v1/products")]
    [InlineData("PUT", "/api/v1/products/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/v1/products/00000000-0000-0000-0000-000000000001")]
    public async Task
        CatalogMutationRoutesAreNotAddressableOrForwarded(
            string method,
            string path)
    {
        fixture.ResetForwardedRequestCount();

        using HttpRequestMessage request = new(
            new HttpMethod(method),
            path);

        using HttpResponseMessage response =
            await _client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.MethodNotAllowed,
            response.StatusCode);

        Assert.Equal(
            0,
            fixture.ForwardedRequestCount);
    }

    [Fact]
    public async Task AuthMeAnonymousReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                "/api/v1/auth/me",
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthMeAuthenticatedReturnsClaims()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/auth/me",
                subject: "alice.customer",
                EshopRoles.Customer);

        using HttpResponseMessage response =
            await _client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        AuthenticatedUserResponse? user =
            await response.Content
                .ReadFromJsonAsync<
                    AuthenticatedUserResponse>(
                    TestContext.Current.CancellationToken);

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
    public async Task BasketAnonymousReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                "/api/v1/basket",
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task BasketCustomerForwardsRequest()
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
    public async Task OrdersCustomerForwardsRequest()
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
    public async Task NotificationsCustomerForwardsRequest()
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
    public async Task BasketSupportReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                subject: "sam.support",
                EshopRoles.Support);

        using HttpResponseMessage response =
            await _client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task OrdersAdminWithoutCustomerRoleReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/orders",
                subject: "anna.admin",
                EshopRoles.Admin);

        using HttpResponseMessage response =
            await _client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task InventoryCustomerReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/inventory-items",
                subject: "alice.customer",
                EshopRoles.Customer);

        using HttpResponseMessage response =
            await _client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task InventorySupportForwardsRequest()
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
    public async Task InventoryAdminForwardsRequest()
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
    public async Task PaymentsSupportForwardsRequest()
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
    public async Task PaymentsAdminForwardsRequest()
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
            await _client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        ForwardedResponse? forwardedResponse =
            await response.Content
                .ReadFromJsonAsync<ForwardedResponse>(Xunit.TestContext.Current.CancellationToken);

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

    private static object?[] CreateRouteCase(
        GatewayRouteContract route,
        string? subject,
        string? role,
        HttpStatusCode expectedStatus,
        bool expectedForwarding)
    {
        return
        [
            route.Id,
            route.SampleMethod,
            route.SamplePath,
            subject,
            role,
            expectedStatus,
            expectedForwarding
        ];
    }

    private static GatewayRouteRegistry
        LoadRouteRegistry()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "gateway-route-policy.json");

        using FileStream stream =
            File.OpenRead(path);

        return JsonSerializer.Deserialize<
                GatewayRouteRegistry>(
                stream)
            ?? throw new InvalidOperationException(
                "Gateway route policy could not be deserialized.");
    }

    private sealed record
        AuthenticatedUserResponse(
            string? Subject,
            string? PreferredUsername,
            string? Email,
            string[] Roles);

    private sealed record GatewayRouteRegistry(
        [property: JsonPropertyName("routes")]
        GatewayRouteContract[] Routes);

    private sealed record GatewayRouteContract(
        [property: JsonPropertyName("id")]
        string Id,
        [property: JsonPropertyName("kind")]
        string Kind,
        [property: JsonPropertyName("sample_method")]
        string SampleMethod,
        [property: JsonPropertyName("sample_path")]
        string SamplePath,
        [property: JsonPropertyName("authorization_policy")]
        string? AuthorizationPolicy,
        [property: JsonPropertyName("allowed_roles")]
        string[] AllowedRoles);
}
