using System.Text.Json.Serialization;

namespace BasketService.Domain;

public sealed record BasketItem(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity)
{
    [JsonIgnore]
    public decimal LineTotal => UnitPrice * Quantity;
}
