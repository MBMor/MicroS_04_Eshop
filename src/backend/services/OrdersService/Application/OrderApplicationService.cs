using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Integration;
using OrdersService.Outbox;
using Eshop.Contracts.IntegrationEvents.V1;
using Eshop.Messaging.RabbitMq;

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
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        string normalizedCustomerEmail =
            customerEmail.Trim();

        string normalizedPaymentMethod =
            paymentMethod.Trim().ToLowerInvariant();

        string requestFingerprint =
            CreateRequestFingerprint(
                normalizedCustomerEmail,
                normalizedPaymentMethod);

        CreateOrderResult? existingResult =
            await ResolveExistingCommandAsync(
                customerId,
                idempotencyKey,
                requestFingerprint,
                cancellationToken);

        if (existingResult is not null)
        {
            return existingResult;
        }

        BasketSnapshot basket = await basketClient.GetBasketAsync(
            customerId,
            cancellationToken);

        if (basket.Items.Length == 0)
        {
            return await ResolveExistingCommandAsync(
                    customerId,
                    idempotencyKey,
                    requestFingerprint,
                    cancellationToken)
                ?? CreateOrderResult.EmptyBasket();
        }

        string[] currencies = basket.Items
            .Select(item => item.Currency.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (currencies.Length != 1)
        {
            return await ResolveExistingCommandAsync(
                    customerId,
                    idempotencyKey,
                    requestFingerprint,
                    cancellationToken)
                ?? CreateOrderResult.MultipleCurrencies();
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
            normalizedCustomerEmail,
            normalizedPaymentMethod,
            orderItems,
            timeProvider.GetUtcNow());

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

        OrderIdempotencyRecord idempotencyRecord =
            OrderIdempotencyRecord.CreateCompleted(
                Guid.NewGuid(),
                customerId,
                idempotencyKey,
                requestFingerprint,
                order.Id,
                occurredAtUtc);

        dbContext.Orders.Add(order);
        dbContext.OutboxMessages.Add(outboxMessage);
        dbContext.OrderIdempotencyRecords.Add(
            idempotencyRecord);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateIdempotencyCommand(exception))
        {
            dbContext.ChangeTracker.Clear();

            return await ResolveExistingCommandAsync(
                    customerId,
                    idempotencyKey,
                    requestFingerprint,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The conflicting idempotency command could not be resolved.",
                    exception);
        }

        await TryClearBasketAsync(
            customerId,
            order.Id,
            cancellationToken);

        return CreateOrderResult.Succeeded(order);
    }

    private async Task<CreateOrderResult?>
        ResolveExistingCommandAsync(
            string customerId,
            string idempotencyKey,
            string requestFingerprint,
            CancellationToken cancellationToken)
    {
        OrderIdempotencyRecord? record =
            await dbContext.OrderIdempotencyRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.CustomerId == customerId
                        && candidate.Operation
                            == OrderIdempotencyRecord
                                .CreateOrderOperation
                        && candidate.IdempotencyKey
                            == idempotencyKey,
                    cancellationToken);

        if (record is null)
        {
            return null;
        }

        if (!string.Equals(
                record.RequestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            return CreateOrderResult.IdempotencyConflict();
        }

        Order? order = await dbContext.Orders
            .AsNoTracking()
            .Include(candidate => candidate.Items)
            .Include(candidate => candidate.StatusHistory)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == record.OrderId
                    && candidate.CustomerId == customerId,
                cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException(
                $"Completed idempotency record '{record.Id}' " +
                "does not reference an accessible order.");
        }

        return CreateOrderResult.Succeeded(
            order,
            isReplay: true);
    }

    private static string CreateRequestFingerprint(
        string customerEmail,
        string paymentMethod)
    {
        byte[] canonicalRequest =
            JsonSerializer.SerializeToUtf8Bytes(
                new CanonicalCreateOrderRequest(
                    customerEmail,
                    paymentMethod));

        return Convert.ToHexString(
            SHA256.HashData(canonicalRequest));
    }

    private static bool IsDuplicateIdempotencyCommand(
        DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName:
                    OrderIdempotencyRecord
                        .UniqueCommandIndexName
            };
    }

    private sealed record CanonicalCreateOrderRequest(
        string CustomerEmail,
        string PaymentMethod);

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

    public async Task<OperationalOrderPage>
        ListOperationalAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken)
    {
        List<Order> orders =
            await dbContext.Orders
                .AsNoTracking()
                .Include(order => order.Items)
                .OrderByDescending(
                    order => order.CreatedAtUtc)
                .ThenByDescending(
                    order => order.Id)
                .Skip(offset)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);

        bool hasMore =
            orders.Count > limit;

        if (hasMore)
        {
            orders.RemoveAt(limit);
        }

        return new OperationalOrderPage(
            orders,
            offset,
            limit,
            hasMore);
    }

    public Task<Order?> GetOperationalAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .FirstOrDefaultAsync(
                order => order.Id == orderId,
                cancellationToken);
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
