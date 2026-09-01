using NotificationsService.Domain;

namespace NotificationsService.Contracts;

public sealed record OperationalNotificationResponse(
    Guid Id,
    string CustomerId,
    Guid? OrderId,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    Guid? SourceEventId,
    Guid? CorrelationId)
{
    public static OperationalNotificationResponse FromNotification(
        Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new OperationalNotificationResponse(
            notification.Id,
            notification.CustomerId,
            notification.OrderId,
            notification.Type.ToString(),
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.CreatedAtUtc,
            notification.ReadAtUtc,
            notification.SourceEventId,
            notification.CorrelationId);
    }
}
