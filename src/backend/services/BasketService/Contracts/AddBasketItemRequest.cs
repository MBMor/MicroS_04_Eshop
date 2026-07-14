using System.ComponentModel.DataAnnotations;

namespace BasketService.Contracts;

public sealed class AddBasketItemRequest
{
    public Guid ProductId { get; init; }

    [Range(1, 100)]
    public int Quantity { get; init; } = 1;
}
