namespace Eshop.Operations.Desktop.Api.Orders;

public interface IOrdersApiClient
{
    Task<OperationalOrderPageDto> GetOrdersAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken);

    Task<OperationalOrderDetailDto> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken);
}
