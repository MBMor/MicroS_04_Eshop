using System.Security.Claims;
using Microsoft.Extensions.Options;
using NotificationsService.Options;

namespace NotificationsService.Identity;

public sealed class NotificationOwnerProvider(
    IWebHostEnvironment environment,
    IOptions<NotificationsOptions> notificationsOptions)
    : INotificationOwnerProvider
{
    private const int MaxCustomerIdLength = 128;

    private readonly NotificationsOptions _notificationsOptions =
        notificationsOptions.Value;

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
            || !_notificationsOptions
                .AllowDevelopmentCustomerHeader)
        {
            return null;
        }

        if (!httpContext.Request.Headers.TryGetValue(
                _notificationsOptions
                    .DevelopmentCustomerHeaderName,
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
