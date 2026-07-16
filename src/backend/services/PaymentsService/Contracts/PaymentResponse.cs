using PaymentsService.Domain;

namespace PaymentsService.Contracts;

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Status,
    string? FailureReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ProcessedAtUtc)
{
    public static PaymentResponse FromPayment(
        Payment payment)
    {
        return new PaymentResponse(
            payment.Id,
            payment.OrderId,
            payment.CustomerId,
            payment.Amount,
            payment.Currency,
            payment.PaymentMethod,
            payment.Status.ToString(),
            payment.FailureReason,
            payment.CreatedAtUtc,
            payment.ProcessedAtUtc);
    }
}
