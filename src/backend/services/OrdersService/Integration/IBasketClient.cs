namespace OrdersService.Integration;

public interface IBasketClient
{
    Task<BasketSnapshot> GetBasketAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task ClearBasketAsync(
        string customerId,
        CancellationToken cancellationToken);
}
