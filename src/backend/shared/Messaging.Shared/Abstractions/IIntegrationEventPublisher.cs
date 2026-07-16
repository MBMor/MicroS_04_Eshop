using Eshop.Contracts.IntegrationEvents;
using Messaging.Shared.Contracts;

namespace Messaging.Shared.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        string routingKey,
        MessagePublishContext publishContext,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
