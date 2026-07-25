using Eshop.Contracts.IntegrationEvents;
using Eshop.Messaging.Contracts;

namespace Eshop.Messaging.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        string routingKey,
        MessagePublishContext publishContext,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
