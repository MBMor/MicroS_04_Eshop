using System.ComponentModel.DataAnnotations;

namespace PaymentsService.Contracts;

public sealed class CreatePaymentRequest
{
    public Guid OrderId { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public required string CustomerId { get; init; }

    [Range(
        typeof(decimal),
        "0.01",
        "999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Amount must be between 0.01 and 999999999.99.")]
    public decimal Amount { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "CZK";

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string PaymentMethod { get; init; } = "test-success";
}
