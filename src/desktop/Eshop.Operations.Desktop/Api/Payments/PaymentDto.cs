namespace Eshop.Operations.Desktop.Api.Payments;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Status,
    string? FailureReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ProcessedAtUtc);
