using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Integration;

namespace OrdersService.Application;

public sealed class OrderApplicationService(
    OrdersDbContext dbContext,
    IBasketClient basketClient,
    ILogger<OrderApplicationService> logger)
{
    public async Task<CreateOrderResult> CreateAsync(
        string customerId,
        string customerEmail,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        BasketSnapshot basket = await basketClient.GetBasketAsync(
            customerId,
            cancellationToken);

        if (basket.Items.Length == 0)
        {
            return CreateOrderResult.EmptyBasket();
        }

        string[] currencies = basket.Items
            .Select(item => item.Currency.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (currencies.Length != 1)
        {
            return CreateOrderResult.MultipleCurrencies();
        }

        Guid orderId = Guid.NewGuid();

        OrderItem[] orderItems = basket.Items
            .Select(item => OrderItem.Create(
                Guid.NewGuid(),
                orderId,
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Currency,
                item.Quantity))
            .ToArray();

        Order order = Order.Create(
            orderId,
            customerId,
            customerEmail,
            paymentMethod,
            orderItems,
            DateTimeOffset.UtcNow);

        dbContext.Orders.Add(order);

        await dbContext.SaveChangesAsync(cancellationToken);

        await TryClearBasketAsync(
            customerId,
            order.Id,
            cancellationToken);

        return CreateOrderResult.Succeeded(order);
    }

    public Task<Order?> GetAsync(
        string customerId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order =>
                    order.Id == orderId
                    && order.CustomerId == customerId,
                cancellationToken);
    }

    public Task<List<Order>> ListAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    private async Task TryClearBasketAsync(
        string customerId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await basketClient.ClearBasketAsync(
                customerId,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Order {OrderId} was created, but the basket could not be cleared.",
                orderId);
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Order {OrderId} was created, but clearing the basket timed out.",
                orderId);
        }
    }
}
