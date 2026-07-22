using System.Security.Claims;
using Eshop.Security.Authorization;

namespace BasketService.Identity;

public sealed class BasketOwnerProvider
    : IBasketOwnerProvider
{
    private const int MaxCustomerIdLength = 128;

    public string? GetCustomerId(
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string? subject = httpContext.User.FindFirstValue(
            EshopClaimNames.Subject);

        return Normalize(subject);
    }

    private static string? Normalize(
        string? customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        string normalizedCustomerId =
            customerId.Trim();

        return normalizedCustomerId.Length
               <= MaxCustomerIdLength
            ? normalizedCustomerId
            : null;
    }
}
