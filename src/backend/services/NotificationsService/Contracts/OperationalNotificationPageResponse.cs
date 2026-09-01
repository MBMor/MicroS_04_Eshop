namespace NotificationsService.Contracts;

public sealed record OperationalNotificationPageResponse(
    IReadOnlyList<OperationalNotificationResponse> Items,
    int Offset,
    int Limit,
    bool HasMore);
