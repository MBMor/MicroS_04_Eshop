using System.ComponentModel.DataAnnotations;

namespace BasketService.Contracts;

public sealed class UpdateBasketItemRequest
{
    [Range(1, 100)]
    public int Quantity { get; init; }
}
