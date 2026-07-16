using RabbitMQ.Client;

namespace Messaging.Shared.RabbitMq;

public interface IRabbitMqConnectionProvider : IAsyncDisposable
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}
