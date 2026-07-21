using Eshop.Contracts.IntegrationEvents.V1;

namespace OrdersService.Application;

public interface IOrderStockResultService
{
    Task ApplyStockReservedAsync(
        StockReservedV1 integrationEvent,
        CancellationToken cancellationToken);

    Task ApplyStockReservationFailedAsync(
        StockReservationFailedV1 integrationEvent,
        CancellationToken cancellationToken);
}
