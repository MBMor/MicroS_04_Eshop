using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Domain;

namespace OrdersService.Application;

public sealed class OrderStockResultService(
    OrdersDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task ApplyStockReservedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        Order? order = await dbContext.Orders
            .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException($"Order '{orderId}' does not exist.");
        }

        order.MarkStockReserved(timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyStockReservationFailedAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken)
    {
        Order? order = await dbContext.Orders
            .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException($"Order '{orderId}' does not exist.");
        }

        order.MarkStockReservationFailed(reason, timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
