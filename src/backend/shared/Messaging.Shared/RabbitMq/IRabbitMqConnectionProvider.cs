using RabbitMQ.Client;

namespace Eshop.Messaging.RabbitMq;

public interface IRabbitMqConnectionProvider : IAsyncDisposable
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}
