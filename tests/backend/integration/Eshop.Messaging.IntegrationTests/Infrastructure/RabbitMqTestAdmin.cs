using Messaging.Shared.RabbitMq;
using RabbitMQ.Client;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public sealed class RabbitMqTestAdmin(
    MessagingSystemFixture fixture)
{
    private static readonly string[] MainQueueNames =
        RabbitMqTopology.Bindings
            .Select(binding => binding.QueueName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(
                queueName => queueName,
                StringComparer.Ordinal)
            .ToArray();

    private static readonly string[] AllQueueNames =
        MainQueueNames
            .Concat(
                MainQueueNames.Select(
                    RabbitMqQueues.DeadLetter))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(
                queueName => queueName,
                StringComparer.Ordinal)
            .ToArray();

    public async Task PurgeAllAsync(
        CancellationToken cancellationToken = default)
    {
        ConnectionFactory connectionFactory =
            RabbitMqTestTopology.CreateConnectionFactory(
                fixture);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-test-purge",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        foreach (string queueName in AllQueueNames)
        {
            await channel.QueuePurgeAsync(
                queueName,
                cancellationToken);
        }
    }

    public async Task<uint> GetReadyMessageCountAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            queueName);

        ConnectionFactory connectionFactory =
            RabbitMqTestTopology.CreateConnectionFactory(
                fixture);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-test-count",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        return await channel.MessageCountAsync(
            queueName,
            cancellationToken);
    }

    public async Task<Dictionary<string, uint>>
        GetReadyMessageCountsAsync(
            bool includeDeadLetterQueues,
            CancellationToken cancellationToken = default)
    {
        string[] queueNames =
            includeDeadLetterQueues
                ? AllQueueNames
                : MainQueueNames;

        ConnectionFactory connectionFactory =
            RabbitMqTestTopology.CreateConnectionFactory(
                fixture);

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                "messaging-integration-test-count-all",
                cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        Dictionary<string, uint> counts =
            new(
                queueNames.Length,
                StringComparer.Ordinal);

        foreach (string queueName in queueNames)
        {
            counts[queueName] =
                await channel.MessageCountAsync(
                    queueName,
                    cancellationToken);
        }

        return counts;
    }

    public async Task<bool> MainQueuesAreEmptyAsync(
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, uint> counts =
            await GetReadyMessageCountsAsync(
                includeDeadLetterQueues: false,
                cancellationToken);

        return counts.Values.All(
            count => count == 0);
    }
}
