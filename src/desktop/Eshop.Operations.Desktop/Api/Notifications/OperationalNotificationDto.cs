namespace Eshop.Operations.Desktop.Api.Notifications;

public sealed record OperationalNotificationDto(
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
    Guid? CorrelationId);