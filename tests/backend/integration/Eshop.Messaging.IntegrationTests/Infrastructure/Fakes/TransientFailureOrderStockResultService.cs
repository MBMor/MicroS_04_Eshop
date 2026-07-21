using Eshop.Contracts.IntegrationEvents.V1;
using OrdersService.Application;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;

public sealed class TransientFailureOrderStockResultService(
    OrderStockResultService innerService,
    TransientConsumerFailureState failureState)
    : IOrderStockResultService
{
    public Task ApplyStockReservedAsync(
        StockReservedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            integrationEvent);

        ThrowIfFailureIsConfigured(
            integrationEvent.EventId);

        return innerService.ApplyStockReservedAsync(
            integrationEvent,
            cancellationToken);
    }

    public Task ApplyStockReservationFailedAsync(
        StockReservationFailedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            integrationEvent);

        ThrowIfFailureIsConfigured(
            integrationEvent.EventId);

        return innerService
            .ApplyStockReservationFailedAsync(
                integrationEvent,
                cancellationToken);
    }

    private void ThrowIfFailureIsConfigured(
        Guid eventId)
    {
        if (!failureState.RecordAttemptAndShouldFail(eventId))
        {
            return;
        }

        throw new TimeoutException(
            $"Simulated transient consumer failure " +
            $"for event '{eventId}'.");
    }
}
