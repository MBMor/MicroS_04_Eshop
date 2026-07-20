using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Integration;
using OrdersService.Outbox;
using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.RabbitMq;

namespace OrdersService.Application;

public sealed class OrderApplicationService(
    OrdersDbContext dbContext,
    IBasketClient basketClient,
    OrdersOutboxWriter outboxWriter,
    TimeProvider timeProvider,
    ILogger<OrderApplicationService> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> LogBasketClearFailed =
    LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(2500, nameof(LogBasketClearFailed)),
        "Order {OrderId} was created, but the basket could not be cleared.");

    private static readonly Action<ILogger, Guid, Exception?> LogBasketClearTimedOut =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(2501, nameof(LogBasketClearTimedOut)),
            "Order {OrderId} was created, but clearing the basket timed out.");

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

        Guid correlationId = Guid.NewGuid();
        DateTimeOffset occurredAtUtc = timeProvider.GetUtcNow();

        OrderCreatedV1 orderCreated = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: occurredAtUtc,
            CorrelationId: correlationId,
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            TotalAmount: order.TotalAmount,
            Currency: order.Currency,
            Items: order.Items
                .Select(item => new OrderCreatedItemV1(
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice))
                .ToArray());

        OutboxMessage outboxMessage = outboxWriter.Create(
            orderCreated,
            RabbitMqRoutingKeys.OrderCreatedV1);

        dbContext.Orders.Add(order);
        dbContext.OutboxMessages.Add(outboxMessage);

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
            .Include(order => order.StatusHistory)
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
            LogBasketClearFailed(
                logger,
                orderId,
                exception);
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            LogBasketClearTimedOut(
                logger,
                orderId,
                exception);
        }
    }
}
