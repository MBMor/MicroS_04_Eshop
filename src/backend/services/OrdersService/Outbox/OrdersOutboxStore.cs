using Messaging.Shared.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using OrdersService.Data;

namespace OrdersService.Outbox;

public sealed class OrdersOutboxStore(
    OrdersDbContext dbContext,
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

        DateTimeOffset now =
            timeProvider.GetUtcNow();

        DateTimeOffset staleClaimThreshold =
            now - _options.ClaimTimeout;

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
                        status = {pendingStatus}
                        OR
                        (
                            status = {failedStatus}
                            AND retry_count < {_options.MaximumRetryCount}
                            AND
                            (
                                next_attempt_at_utc IS NULL
                                OR next_attempt_at_utc <= {now}
                            )
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
                    ORDER BY
                        CASE
                            WHEN status = {failedStatus}
                                THEN COALESCE(
                                    next_attempt_at_utc,
                                    occurred_at_utc)
                            ELSE occurred_at_utc
                        END,
                        occurred_at_utc
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
                now);

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
                            message => message.NextAttemptAtUtc,
                            (DateTimeOffset?)null)
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

        int? currentRetryCount =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message =>
                    message.Id == messageId
                    && message.Status
                        == OutboxMessageStatus.Processing
                    && message.ClaimedBy == workerId)
                .Select(message =>
                    (int?)message.RetryCount)
                .SingleOrDefaultAsync(cancellationToken);

        if (!currentRetryCount.HasValue)
        {
            return false;
        }

        int nextRetryCount =
            checked(currentRetryCount.Value + 1);

        bool retryLimitReached =
            nextRetryCount
            >= _options.MaximumRetryCount;

        OutboxMessageStatus nextStatus =
            retryLimitReached
                ? OutboxMessageStatus.Dead
                : OutboxMessageStatus.Failed;

        DateTimeOffset? nextAttemptAtUtc =
            retryLimitReached
                ? null
                : timeProvider.GetUtcNow()
                  + OutboxRetryPolicy.CalculateDelay(
                      _options,
                      currentRetryCount.Value);

        int affectedRows =
            await dbContext.OutboxMessages
                .Where(message =>
                    message.Id == messageId
                    && message.Status
                        == OutboxMessageStatus.Processing
                    && message.ClaimedBy == workerId
                    && message.RetryCount
                        == currentRetryCount.Value)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            message => message.Status,
                            nextStatus)
                        .SetProperty(
                            message => message.RetryCount,
                            nextRetryCount)
                        .SetProperty(
                            message => message.LastError,
                            normalizedError)
                        .SetProperty(
                            message => message.PublishedAtUtc,
                            (DateTimeOffset?)null)
                        .SetProperty(
                            message => message.NextAttemptAtUtc,
                            nextAttemptAtUtc)
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

        return normalizedError.Length
            <= MaximumErrorLength
                ? normalizedError
                : normalizedError[..MaximumErrorLength];
    }
}
