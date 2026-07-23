namespace PaymentsService.Domain;

public sealed class Payment
{
    private const int MaxCustomerIdLength = 128;
    private const int MaxPaymentMethodLength = 64;
    private const int MaxFailureReasonLength = 500;

    private Payment()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string CustomerId { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string PaymentMethod { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public static Payment CreatePending(
        Guid id,
        Guid orderId,
        string customerId,
        decimal amount,
        string currency,
        string paymentMethod,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Payment id must not be empty.",
                nameof(id));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must not be empty.",
                nameof(orderId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Payment amount must be greater than zero.");
        }

        string normalizedCurrency = RequiredTrimmed(
            currency,
            nameof(currency),
            3);

        if (normalizedCurrency.Length != 3)
        {
            throw new ArgumentException(
                "Currency must contain exactly three characters.",
                nameof(currency));
        }

        return new Payment
        {
            Id = id,
            OrderId = orderId,
            CustomerId = RequiredTrimmed(
                customerId,
                nameof(customerId),
                MaxCustomerIdLength),
            Amount = amount,
            Currency = normalizedCurrency.ToUpperInvariant(),
            PaymentMethod = RequiredTrimmed(
                    paymentMethod,
                    nameof(paymentMethod),
                    MaxPaymentMethodLength)
                .ToLowerInvariant(),
            Status = PaymentStatus.Pending,
            FailureReason = null,
            CreatedAtUtc = createdAtUtc,
            ProcessedAtUtc = null
        };
    }

    public void Authorize(DateTimeOffset processedAtUtc)
    {
        EnsurePending();

        Status = PaymentStatus.Authorized;
        FailureReason = null;
        ProcessedAtUtc = processedAtUtc;
    }

    public void Fail(
        string failureReason,
        DateTimeOffset processedAtUtc)
    {
        EnsurePending();

        string normalizedFailureReason = RequiredTrimmed(
            failureReason,
            nameof(failureReason),
            MaxFailureReasonLength);

        Status = PaymentStatus.Failed;
        FailureReason = normalizedFailureReason;
        ProcessedAtUtc = processedAtUtc;
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Payment in status '{Status}' cannot be processed again.");
        }
    }

    private static string RequiredTrimmed(
        string value,
        string parameterName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value must not be empty.",
                parameterName);
        }

        string normalizedValue = value.Trim();

        if (normalizedValue.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value must not exceed {maxLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }
}
