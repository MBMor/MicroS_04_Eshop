using BasketService.Domain;

namespace BasketService.Application;

public enum BasketMutationStatus
{
    Success,
    NotFound,
    ValidationFailed
}

public sealed record BasketMutationResult(
    BasketMutationStatus Status,
    ShoppingBasket? Basket,
    string? Error)
{
    public static BasketMutationResult Succeeded(ShoppingBasket basket)
    {
        return new BasketMutationResult(
            BasketMutationStatus.Success,
            basket,
            null);
    }

    public static BasketMutationResult NotFound(string error)
    {
        return new BasketMutationResult(
            BasketMutationStatus.NotFound,
            null,
            error);
    }

    public static BasketMutationResult ValidationFailed(string error)
    {
        return new BasketMutationResult(
            BasketMutationStatus.ValidationFailed,
            null,
            error);
    }
}
