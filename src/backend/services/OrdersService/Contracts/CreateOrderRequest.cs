using System.ComponentModel.DataAnnotations;

namespace OrdersService.Contracts;

public sealed class CreateOrderRequest
{
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public required string CustomerEmail { get; init; }

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string PaymentMethod { get; init; } = "test-success";
}
