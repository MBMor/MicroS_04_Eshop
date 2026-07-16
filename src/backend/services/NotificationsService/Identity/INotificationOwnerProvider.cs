namespace NotificationsService.Identity;

public interface INotificationOwnerProvider
{
    string? GetCustomerId(HttpContext httpContext);
}
