using BasketService.Domain;

namespace BasketService.Contracts;

public sealed record BasketResponse(
    BasketItemResponse[] Items,
    BasketTotalResponse[] Totals,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public static BasketResponse FromBasket(
        ShoppingBasket basket,
        int expirationMinutes)
    {
        BasketItemResponse[] items = basket.Items
            .Select(item => new BasketItemResponse(
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Currency,
                item.Quantity,
                item.LineTotal))
            .ToArray();

        BasketTotalResponse[] totals = basket.Items
            .GroupBy(
                item => item.Currency,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new BasketTotalResponse(
                group.Key,
                group.Sum(item => item.LineTotal)))
            .OrderBy(total => total.Currency)
            .ToArray();

        return new BasketResponse(
            items,
            totals,
            basket.UpdatedAtUtc,
            basket.UpdatedAtUtc.AddMinutes(expirationMinutes));
    }
}

public sealed record BasketItemResponse(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal LineTotal);

public sealed record BasketTotalResponse(
    string Currency,
    decimal Amount);
