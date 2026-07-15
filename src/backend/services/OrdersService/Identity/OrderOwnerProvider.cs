using System.Security.Claims;
using Microsoft.Extensions.Options;
using OrdersService.Options;

namespace OrdersService.Identity;

public sealed class OrderOwnerProvider(
    IWebHostEnvironment environment,
    IOptions<OrdersOptions> ordersOptions) : IOrderOwnerProvider
{
    private const int MaxCustomerIdLength = 128;

    private readonly OrdersOptions _ordersOptions =
        ordersOptions.Value;

    public string? GetCustomerId(HttpContext httpContext)
    {
        string? authenticatedCustomerId =
            httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        string? normalizedCustomerId =
            Normalize(authenticatedCustomerId);

        if (normalizedCustomerId is not null)
        {
            return normalizedCustomerId;
        }

        if (!environment.IsDevelopment()
            || !_ordersOptions.AllowDevelopmentCustomerHeader)
        {
            return null;
        }

        if (!httpContext.Request.Headers.TryGetValue(
                _ordersOptions.DevelopmentCustomerHeaderName,
                out Microsoft.Extensions.Primitives.StringValues values))
        {
            return null;
        }

        return Normalize(values.ToString());
    }

    private static string? Normalize(string? customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        string normalizedCustomerId = customerId.Trim();

        return normalizedCustomerId.Length <= MaxCustomerIdLength
            ? normalizedCustomerId
            : null;
    }
}
