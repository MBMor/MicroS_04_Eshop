using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Messaging.Shared.RabbitMq;

public sealed class RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options) : IRabbitMqConnectionProvider
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            ConnectionFactory factory = new()
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
                TopologyRecoveryEnabled = _options.TopologyRecoveryEnabled,
                RequestedHeartbeat = TimeSpan.FromSeconds(_options.RequestedHeartbeatSeconds)
            };

            _connection = await factory.CreateConnectionAsync(
                _options.ClientProvidedName,
                cancellationToken);

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }
}
