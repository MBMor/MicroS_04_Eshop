namespace Eshop.Operations.Desktop.Api.Notifications;

public sealed record OperationalNotificationPageDto(
    IReadOnlyList<OperationalNotificationDto> Items,
    int Offset,
    int Limit,
    bool HasMore);