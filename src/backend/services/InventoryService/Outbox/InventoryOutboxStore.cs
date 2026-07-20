using InventoryService.Data;
using Messaging.Shared.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace InventoryService.Outbox;

public sealed class InventoryOutboxStore(
    InventoryDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<OutboxProcessingOptions> options)
{
    private const int MaximumErrorLength = 4_000;

    private readonly OutboxProcessingOptions _options =
        options.Value;

    public async Task<List<ClaimedOutboxMessage>> ClaimBatchAsync(
        string workerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        DateTimeOffset claimedAtUtc =
            timeProvider.GetUtcNow();

        DateTimeOffset staleClaimThreshold =
            claimedAtUtc - _options.ClaimTimeout;

        string pendingStatus =
            OutboxMessageStatus.Pending.ToString();

        string failedStatus =
            OutboxMessageStatus.Failed.ToString();

        string processingStatus =
            OutboxMessageStatus.Processing.ToString();

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        List<OutboxMessage> messages =
            await dbContext.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM outbox_messages
                    WHERE
                        status IN (
                            {pendingStatus},
                            {failedStatus}
                        )
                        OR
                        (
                            status = {processingStatus}
                            AND
                            (
                                claimed_at_utc IS NULL
                                OR claimed_at_utc < {staleClaimThreshold}
                            )
                        )
                    ORDER BY occurred_at_utc
                    FOR UPDATE SKIP LOCKED
                    LIMIT {_options.BatchSize}
                    """)
                .AsTracking()
                .ToListAsync(cancellationToken);

        List<ClaimedOutboxMessage> claimedMessages =
            new(messages.Count);

        foreach (OutboxMessage message in messages)
        {
            message.Claim(
                workerId,
                claimedAtUtc);

            claimedMessages.Add(
                new ClaimedOutboxMessage(
                    message.Id,
                    message.EventId,
                    message.CorrelationId,
                    message.EventType,
                    message.RoutingKey,
                    message.Payload,
                    message.TraceParent,
                    message.TraceState));
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return claimedMessages;
    }

    public async Task<bool> MarkPublishedAsync(
        Guid messageId,
        string workerId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        int affectedRows =
            await dbContext.OutboxMessages
                .Where(message =>
                    message.Id == messageId
                    && message.Status
                        == OutboxMessageStatus.Processing
                    && message.ClaimedBy == workerId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            message => message.Status,
                            OutboxMessageStatus.Published)
                        .SetProperty(
                            message => message.PublishedAtUtc,
                            (DateTimeOffset?)publishedAtUtc)
                        .SetProperty(
                            message => message.LastError,
                            (string?)null)
                        .SetProperty(
                            message => message.ClaimedAtUtc,
                            (DateTimeOffset?)null)
                        .SetProperty(
                            message => message.ClaimedBy,
                            (string?)null),
                    cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkFailedAsync(
        Guid messageId,
        string workerId,
        string error,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        string normalizedError =
            NormalizeError(error);

        int affectedRows =
            await dbContext.OutboxMessages
                .Where(message =>
                    message.Id == messageId
                    && message.Status
                        == OutboxMessageStatus.Processing
                    && message.ClaimedBy == workerId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            message => message.Status,
                            OutboxMessageStatus.Failed)
                        .SetProperty(
                            message => message.RetryCount,
                            message => message.RetryCount + 1)
                        .SetProperty(
                            message => message.LastError,
                            normalizedError)
                        .SetProperty(
                            message => message.ClaimedAtUtc,
                            (DateTimeOffset?)null)
                        .SetProperty(
                            message => message.ClaimedBy,
                            (string?)null),
                    cancellationToken);

        return affectedRows == 1;
    }

    private static string NormalizeError(
        string error)
    {
        string normalizedError =
            string.IsNullOrWhiteSpace(error)
                ? "Unknown outbox publishing error."
                : error.Trim();

        return normalizedError.Length <= MaximumErrorLength
            ? normalizedError
            : normalizedError[..MaximumErrorLength];
    }
}
