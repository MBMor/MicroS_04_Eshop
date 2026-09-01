using NotificationsService.Domain;

namespace NotificationsService.Application;

public sealed record OperationalNotificationPage(
    IReadOnlyList<Notification> Items,
    int Offset,
    int Limit,
    bool HasMore);