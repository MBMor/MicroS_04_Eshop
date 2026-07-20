using Messaging.Shared.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrdersService.Data;

namespace OrdersService.Outbox;

public sealed class OrdersOutboxCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<OutboxProcessingOptions> options,
    ILogger<OrdersOutboxCleanupWorker> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?>
        LogMessagesDeleted =
            LoggerMessage.Define<int>(
                LogLevel.Information,
                new EventId(2450, nameof(LogMessagesDeleted)),
                "Deleted {DeletedCount} published orders outbox messages.");

    private static readonly Action<ILogger, Exception?>
        LogCleanupFailed =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(2451, nameof(LogCleanupFailed)),
                "Orders outbox cleanup failed.");

    private readonly OutboxProcessingOptions _options =
        options.Value;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using PeriodicTimer timer =
            new(_options.CleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                try
                {
                    int deletedCount =
                        await DeleteExpiredMessagesAsync(
                            stoppingToken);

                    if (deletedCount > 0)
                    {
                        LogMessagesDeleted(
                            logger,
                            deletedCount,
                            null);
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    LogCleanupFailed(
                        logger,
                        exception);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normální ukončení background workeru.
        }
    }

    private async Task<int> DeleteExpiredMessagesAsync(
        CancellationToken cancellationToken)
    {
        int totalDeletedCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            int deletedCount =
                await DeleteBatchAsync(
                    cancellationToken);

            totalDeletedCount += deletedCount;

            if (deletedCount
                < _options.CleanupBatchSize)
            {
                break;
            }
        }

        return totalDeletedCount;
    }

    private async Task<int> DeleteBatchAsync(
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            scopeFactory.CreateAsyncScope();

        OrdersDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        DateTimeOffset retentionThreshold =
            timeProvider.GetUtcNow()
            - _options.PublishedRetention;

        List<Guid> messageIds =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message =>
                    message.Status
                        == OutboxMessageStatus.Published
                    && message.PublishedAtUtc.HasValue
                    && message.PublishedAtUtc.Value
                        < retentionThreshold)
                .OrderBy(message =>
                    message.PublishedAtUtc)
                .Select(message => message.Id)
                .Take(_options.CleanupBatchSize)
                .ToListAsync(cancellationToken);

        if (messageIds.Count == 0)
        {
            return 0;
        }

        return await dbContext.OutboxMessages
            .Where(message =>
                messageIds.Contains(message.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
