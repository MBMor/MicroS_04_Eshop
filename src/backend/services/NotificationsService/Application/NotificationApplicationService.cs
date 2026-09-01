using Microsoft.EntityFrameworkCore;
using NotificationsService.Data;
using NotificationsService.Domain;

namespace NotificationsService.Application;

public sealed class NotificationApplicationService(
    NotificationsDbContext dbContext)
{
    private const int MaximumPageSize = 100;
    public async Task<OperationalNotificationPage> ListOperationalAsync(
        Guid? orderId,
        string? customerId,
        Guid? correlationId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (limit is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id must not be empty.", nameof(orderId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Correlation id must not be empty.", nameof(correlationId));
        }

        IQueryable<Notification> query = dbContext.Notifications.AsNoTracking();

        if (orderId.HasValue)
        {
            query = query.Where(notification => notification.OrderId == orderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            string normalizedCustomerId = customerId.Trim();
            query = query.Where(notification => notification.CustomerId == normalizedCustomerId);
        }

        if (correlationId.HasValue)
        {
            query = query.Where(notification => notification.CorrelationId == correlationId.Value);
        }

        Notification[] rows = await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip(offset)
            .Take(limit + 1)
            .ToArrayAsync(cancellationToken);

        return new OperationalNotificationPage(
            rows.Take(limit).ToArray(),
            offset,
            limit,
            rows.Length > limit);
    }

    public Task<Notification?> GetOperationalByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Notifications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                notification => notification.Id == notificationId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> ListAsync(
        string customerId,
        bool unreadOnly,
        Guid? orderId,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        if (limit is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                $"Limit must be between 1 and {MaximumPageSize}.");
        }

        IQueryable<Notification> query =
            dbContext.Notifications
                .AsNoTracking()
                .Where(notification =>
                    notification.CustomerId == customerId);

        if (unreadOnly)
        {
            query = query.Where(notification =>
                !notification.IsRead);
        }

        if (orderId.HasValue)
        {
            query = query.Where(notification =>
                notification.OrderId == orderId.Value);
        }

        return await query
            .OrderByDescending(notification =>
                notification.CreatedAtUtc)
            .ThenByDescending(notification =>
                notification.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<Notification?> GetByIdAsync(
        string customerId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        return dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(
                notification =>
                    notification.Id == notificationId
                    && notification.CustomerId == customerId,
                cancellationToken);
    }

    public Task<int> CountUnreadAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        return dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification =>
                    notification.CustomerId == customerId
                    && !notification.IsRead,
                cancellationToken);
    }
}
