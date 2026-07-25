using Eshop.Contracts.IntegrationEvents;
using Eshop.Messaging.Contracts;

namespace Eshop.Messaging.Abstractions;

public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task ConsumeAsync(
        TEvent integrationEvent,
        MessageContext messageContext,
        CancellationToken cancellationToken);
}
