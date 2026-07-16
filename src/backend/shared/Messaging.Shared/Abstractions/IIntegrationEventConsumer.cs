using Eshop.Contracts.IntegrationEvents;
using Messaging.Shared.Contracts;

namespace Messaging.Shared.Abstractions;

public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task ConsumeAsync(
        TEvent integrationEvent,
        MessageContext messageContext,
        CancellationToken cancellationToken);
}
