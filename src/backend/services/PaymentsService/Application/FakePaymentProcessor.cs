namespace PaymentsService.Application;

public sealed class FakePaymentProcessor
{
    public const string SuccessMethod = "test-success";
    public const string FailureMethod = "test-fail";

    public bool TryProcess(
        string? paymentMethod,
        out FakePaymentDecision? decision)
    {
        decision = null;

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            return false;
        }

        string normalizedMethod = paymentMethod
            .Trim()
            .ToLowerInvariant();

        decision = normalizedMethod switch
        {
            SuccessMethod => new FakePaymentDecision(
                normalizedMethod,
                IsAuthorized: true,
                FailureReason: null),

            FailureMethod => new FakePaymentDecision(
                normalizedMethod,
                IsAuthorized: false,
                FailureReason: "Simulated payment failure."),

            _ => null
        };

        return decision is not null;
    }
}

public sealed record FakePaymentDecision(
    string PaymentMethod,
    bool IsAuthorized,
    string? FailureReason);
