using BasketService.Domain;

namespace BasketService.Data;

public interface IBasketRepository
{
    Task<ShoppingBasket?> GetAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task SetAsync(
        ShoppingBasket basket,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string customerId,
        CancellationToken cancellationToken);
}
