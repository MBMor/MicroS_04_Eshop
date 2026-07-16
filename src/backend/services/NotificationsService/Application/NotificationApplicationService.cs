using Microsoft.EntityFrameworkCore;
using NotificationsService.Data;
using NotificationsService.Domain;

namespace NotificationsService.Application;

public sealed class NotificationApplicationService(
    NotificationsDbContext dbContext)
{
    private const int MaximumPageSize = 100;

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
