using PaymentsService.Domain;

namespace PaymentsService.Application;

public enum CreatePaymentStatus
{
    Success,
    Conflict,
    ValidationFailed
}

public sealed record CreatePaymentResult(
    CreatePaymentStatus Status,
    Payment? Payment,
    string? Error)
{
    public static CreatePaymentResult Succeeded(
        Payment payment)
    {
        return new CreatePaymentResult(
            CreatePaymentStatus.Success,
            payment,
            null);
    }

    public static CreatePaymentResult Conflict(
        string error)
    {
        return new CreatePaymentResult(
            CreatePaymentStatus.Conflict,
            null,
            error);
    }

    public static CreatePaymentResult ValidationFailed(
        string error)
    {
        return new CreatePaymentResult(
            CreatePaymentStatus.ValidationFailed,
            null,
            error);
    }
}
