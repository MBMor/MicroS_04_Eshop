using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Messaging.Shared.RabbitMq;

public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> options)
    : IRabbitMqConnectionProvider
{
    private readonly RabbitMqOptions _options =
        options.Value;

    private readonly SemaphoreSlim _connectionLock =
        new(1, 1);

    private IConnection? _connection;

    private bool _disposed;

    public async Task<IConnection> GetConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        IConnection? existingConnection =
            Volatile.Read(ref _connection);

        if (existingConnection is { IsOpen: true })
        {
            return existingConnection;
        }

        await _connectionLock.WaitAsync(
            cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null
                && _options.AutomaticRecoveryEnabled)
            {
                await WaitForRecoveryAsync(
                    _connection,
                    cancellationToken);

                if (_connection.IsOpen)
                {
                    return _connection;
                }
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();

                _connection = null;
            }

            ConnectionFactory factory =
                CreateConnectionFactory();

            _connection =
                await factory.CreateConnectionAsync(
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

        await _connectionLock.WaitAsync();

        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();

                _connection = null;
            }
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }
    }

    private static async Task WaitForRecoveryAsync(
        IConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.IsOpen)
        {
            return;
        }

        TaskCompletionSource recoveryCompleted =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        AsyncEventHandler<AsyncEventArgs>
            recoverySucceededHandler =
                (_, _) =>
                {
                    recoveryCompleted.TrySetResult();

                    return Task.CompletedTask;
                };

        connection.RecoverySucceededAsync +=
            recoverySucceededHandler;

        try
        {
            if (connection.IsOpen)
            {
                return;
            }

            await recoveryCompleted.Task.WaitAsync(
                cancellationToken);
        }
        finally
        {
            connection.RecoverySucceededAsync -=
                recoverySucceededHandler;
        }
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            HostName =
                _options.HostName,

            Port =
                _options.Port,

            UserName =
                _options.UserName,

            Password =
                _options.Password,

            VirtualHost =
                _options.VirtualHost,

            AutomaticRecoveryEnabled =
                _options.AutomaticRecoveryEnabled,

            TopologyRecoveryEnabled =
                _options.TopologyRecoveryEnabled,

            RequestedHeartbeat =
                TimeSpan.FromSeconds(
                    _options.RequestedHeartbeatSeconds),

            NetworkRecoveryInterval =
                TimeSpan.FromSeconds(
                    _options
                        .NetworkRecoveryIntervalSeconds),

            RequestedConnectionTimeout =
                TimeSpan.FromSeconds(
                    _options
                        .RequestedConnectionTimeoutSeconds)
        };
    }
}
