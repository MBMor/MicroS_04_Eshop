using NotificationsService.Domain;

namespace NotificationsService.Contracts;

public sealed record NotificationResponse(
    Guid Id,
    Guid? OrderId,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc)
{
    public static NotificationResponse FromNotification(
        Notification notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.OrderId,
            notification.Type.ToString(),
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.CreatedAtUtc,
            notification.ReadAtUtc);
    }
}
