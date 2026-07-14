using System.Security.Claims;
using BasketService.Options;
using Microsoft.Extensions.Options;

namespace BasketService.Identity;

public sealed class BasketOwnerProvider(
    IWebHostEnvironment environment,
    IOptions<BasketOptions> basketOptions) : IBasketOwnerProvider
{
    private const int MaxCustomerIdLength = 128;

    private readonly BasketOptions _basketOptions = basketOptions.Value;

    public string? GetCustomerId(HttpContext httpContext)
    {
        string? authenticatedCustomerId =
            httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? normalizedCustomerId = Normalize(authenticatedCustomerId);

        if (normalizedCustomerId is not null)
        {
            return normalizedCustomerId;
        }

        if (!environment.IsDevelopment()
            || !_basketOptions.AllowDevelopmentCustomerHeader)
        {
            return null;
        }

        if (!httpContext.Request.Headers.TryGetValue(
                _basketOptions.DevelopmentCustomerHeaderName,
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
