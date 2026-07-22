using System.Net.Http.Json;
using System.Security.Claims;
using Eshop.Security.Authorization;

namespace OrdersService.Integration;

public sealed class BasketClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    IWebHostEnvironment environment)
    : IBasketClient
{
    private const string AuthorizationHeaderName =
        "Authorization";

    private const string TestCustomerHeaderName =
        "X-Customer-Id";

    public async Task<BasketSnapshot> GetBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Get,
            "api/v1/basket",
            customerId);

        using HttpResponseMessage response =
            await httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        BasketSnapshot? basket =
            await response.Content
                .ReadFromJsonAsync<BasketSnapshot>(
                    cancellationToken);

        return basket
               ?? throw new InvalidOperationException(
                   "Basket Service returned an empty response.");
    }

    public async Task ClearBasketAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Delete,
            "api/v1/basket",
            customerId);

        using HttpResponseMessage response =
            await httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            customerId);

        HttpContext httpContext =
            httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "The current authenticated HTTP context " +
                "is not available.");

        string? authenticatedSubject =
            httpContext.User.FindFirstValue(
                EshopClaimNames.Subject);

        if (!string.Equals(
                authenticatedSubject,
                customerId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The order customer does not match " +
                "the authenticated token subject.");
        }

        HttpRequestMessage request =
            new(method, path);

        string authorization =
            httpContext.Request.Headers[
                AuthorizationHeaderName]
            .ToString();

        if (!string.IsNullOrWhiteSpace(authorization))
        {
            request.Headers.TryAddWithoutValidation(
                AuthorizationHeaderName,
                authorization);

            return request;
        }

        if (environment.IsEnvironment("Testing"))
        {
            // Integration tests use their own authentication
            // scheme and therefore do not carry a real JWT.
            request.Headers.TryAddWithoutValidation(
                TestCustomerHeaderName,
                customerId);

            return request;
        }

        request.Dispose();

        throw new InvalidOperationException(
            "The authenticated request does not contain " +
            "an Authorization header that can be propagated " +
            "to Basket Service.");
    }
}
