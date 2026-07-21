using System.Diagnostics;
using Eshop.Messaging.IntegrationTests.Infrastructure.Factories;
using Messaging.Shared.RabbitMq;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public sealed class MessagingSystemFixture :
    IAsyncLifetime,
    IDisposable
{
    public const string OrdersDatabaseName =
        "orders_db";

    public const string InventoryDatabaseName =
        "inventory_db";

    public const string PaymentsDatabaseName =
        "payments_db";

    public const string NotificationsDatabaseName =
        "notifications_db";

    public const string RabbitMqUserName =
        "eshop";

    public const string RabbitMqPassword =
        "eshop_password";

    public const string RabbitMqVirtualHost =
        "/";

    private const string PostgreSqlUserName =
        "eshop";

    private const string PostgreSqlPassword =
        "eshop_password";

    private const string PostgreSqlDefaultDatabase =
        "postgres";

    private static readonly string[] ServiceDatabaseNames =
    [
        OrdersDatabaseName,
        InventoryDatabaseName,
        PaymentsDatabaseName,
        NotificationsDatabaseName
    ];

    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:18")
            .WithDatabase(PostgreSqlDefaultDatabase)
            .WithUsername(PostgreSqlUserName)
            .WithPassword(PostgreSqlPassword)
            .Build();

    private readonly RabbitMqContainer _rabbitMqContainer =
        new RabbitMqBuilder("rabbitmq:4-management")
            .WithUsername(RabbitMqUserName)
            .WithPassword(RabbitMqPassword)
            .Build();

    private OrdersServiceFactory? _ordersFactory;

    private InventoryServiceFactory? _inventoryFactory;

    private PaymentsServiceFactory? _paymentsFactory;

    private NotificationsServiceFactory? _notificationsFactory;

    private int _disposeStarted;

    public string OrdersConnectionString =>
        CreateDatabaseConnectionString(
            OrdersDatabaseName);

    public string InventoryConnectionString =>
        CreateDatabaseConnectionString(
            InventoryDatabaseName);

    public string PaymentsConnectionString =>
        CreateDatabaseConnectionString(
            PaymentsDatabaseName);

    public string NotificationsConnectionString =>
        CreateDatabaseConnectionString(
            NotificationsDatabaseName);

    public string RabbitMqHostName =>
        _rabbitMqContainer.Hostname;

    public int RabbitMqPort =>
        _rabbitMqContainer.GetMappedPublicPort(
            RabbitMqBuilder.RabbitMqPort);

    public OrdersServiceFactory OrdersFactory =>
        _ordersFactory
        ?? throw new InvalidOperationException(
            "Orders service factory has not been initialized.");

    public InventoryServiceFactory InventoryFactory =>
        _inventoryFactory
        ?? throw new InvalidOperationException(
            "Inventory service factory has not been initialized.");

    public PaymentsServiceFactory PaymentsFactory =>
        _paymentsFactory
        ?? throw new InvalidOperationException(
            "Payments service factory has not been initialized.");

    public NotificationsServiceFactory NotificationsFactory =>
        _notificationsFactory
        ?? throw new InvalidOperationException(
            "Notifications service factory has not been initialized.");

    public Task ResetAsync(
        CancellationToken cancellationToken = default)
    {
        return MessagingTestReset.ResetAsync(
            this,
            cancellationToken);
    }

    public Task StopRabbitMqAsync(
        CancellationToken cancellationToken = default)
    {
        return _rabbitMqContainer.StopAsync(
            cancellationToken);
    }

    public async Task StartRabbitMqAsync(
        CancellationToken cancellationToken = default)
    {
        await _rabbitMqContainer.StartAsync(
            cancellationToken);

        await RabbitMqTestTopology.DeclareAsync(
            this,
            cancellationToken);
    }

    public async Task RestartServiceHostsAsync(
        CancellationToken cancellationToken = default)
    {
        DisposeServiceFactories();

        CreateServiceFactories();

        StartServiceHosts();

        await WaitForConsumersAsync(
            cancellationToken);
    }

    public async Task InitializeAsync()
    {
        using CancellationTokenSource timeout =
            new(TimeSpan.FromMinutes(5));

        try
        {
            await Task.WhenAll(
                _postgresContainer.StartAsync(
                    timeout.Token),
                _rabbitMqContainer.StartAsync(
                    timeout.Token));

            await CreateServiceDatabasesAsync(
                timeout.Token);

            await DatabaseMigrationRunner.ApplyAsync(
                this,
                timeout.Token);

            await RabbitMqTestTopology.DeclareAsync(
                this,
                timeout.Token);

            CreateServiceFactories();

            StartServiceHosts();

            await WaitForConsumersAsync(
                timeout.Token);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposeStarted,
                1) != 0)
        {
            return;
        }

        try
        {
            DisposeServiceFactories();
        }
        finally
        {
            await Task.WhenAll(
                _rabbitMqContainer
                    .DisposeAsync()
                    .AsTask(),
                _postgresContainer
                    .DisposeAsync()
                    .AsTask());
        }
    }

    public void Dispose()
    {
        DisposeAsync()
            .GetAwaiter()
            .GetResult();
    }

    private void CreateServiceFactories()
    {
        _ordersFactory =
            new OrdersServiceFactory(this);

        _inventoryFactory =
            new InventoryServiceFactory(this);

        _paymentsFactory =
            new PaymentsServiceFactory(this);

        _notificationsFactory =
            new NotificationsServiceFactory(this);
    }

    private void StartServiceHosts()
    {
        _ = OrdersFactory.Services;
        _ = InventoryFactory.Services;
        _ = PaymentsFactory.Services;
        _ = NotificationsFactory.Services;
    }

    private void DisposeServiceFactories()
    {
        _notificationsFactory?.Dispose();
        _paymentsFactory?.Dispose();
        _inventoryFactory?.Dispose();
        _ordersFactory?.Dispose();

        _notificationsFactory = null;
        _paymentsFactory = null;
        _inventoryFactory = null;
        _ordersFactory = null;
    }

    private async Task WaitForConsumersAsync(
        CancellationToken cancellationToken)
    {
        ConnectionFactory connectionFactory =
            RabbitMqTestTopology.CreateConnectionFactory(
                this);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-test-readiness",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        TimeSpan readinessTimeout =
            TimeSpan.FromSeconds(30);

        long startedAt =
            Stopwatch.GetTimestamp();

        while (true)
        {
            List<string> queuesWithoutConsumer = [];

            foreach (
                RabbitMqBindingDefinition binding
                in RabbitMqTopology.Bindings)
            {
                uint consumerCount =
                    await channel.ConsumerCountAsync(
                        binding.QueueName,
                        cancellationToken);

                if (consumerCount == 0)
                {
                    queuesWithoutConsumer.Add(
                        binding.QueueName);
                }
            }

            if (queuesWithoutConsumer.Count == 0)
            {
                return;
            }

            if (Stopwatch.GetElapsedTime(startedAt)
                >= readinessTimeout)
            {
                throw new TimeoutException(
                    "RabbitMQ consumers were not registered " +
                    $"within {readinessTimeout}. Queues without " +
                    $"a consumer: {string.Join(
                        ", ",
                        queuesWithoutConsumer)}.");
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(100),
                cancellationToken);
        }
    }

    private async Task CreateServiceDatabasesAsync(
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection =
            new(_postgresContainer.GetConnectionString());

        await connection.OpenAsync(
            cancellationToken);

        foreach (
            string databaseName
            in ServiceDatabaseNames)
        {
            bool databaseExists =
                await DatabaseExistsAsync(
                    connection,
                    databaseName,
                    cancellationToken);

            if (databaseExists)
            {
                continue;
            }

            await CreateDatabaseAsync(
                connection,
                databaseName,
                cancellationToken);
        }
    }

    private static async Task<bool> DatabaseExistsAsync(
        NpgsqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText = """
            SELECT EXISTS
            (
                SELECT 1
                FROM pg_database
                WHERE datname = @database_name
            );
            """;

        command.Parameters.AddWithValue(
            "database_name",
            databaseName);

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return result is true;
    }

    private static async Task CreateDatabaseAsync(
        NpgsqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
            $"CREATE DATABASE {QuoteIdentifier(databaseName)};";

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private string CreateDatabaseConnectionString(
        string databaseName)
    {
        NpgsqlConnectionStringBuilder builder =
            new(_postgresContainer.GetConnectionString())
            {
                Database = databaseName,
                ApplicationName =
                    "eshop-messaging-integration-tests",
                IncludeErrorDetail = true,
                Timeout = 15,
                CommandTimeout = 30
            };

        return builder.ConnectionString;
    }

    private static string QuoteIdentifier(
        string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            identifier);

        return $"\"{identifier.Replace(
            "\"",
            "\"\"",
            StringComparison.Ordinal)}\"";
    }
}
