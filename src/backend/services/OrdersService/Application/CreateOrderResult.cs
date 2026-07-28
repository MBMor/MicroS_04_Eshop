using OrdersService.Domain;

namespace OrdersService.Application;

public enum CreateOrderStatus
{
    Success,
    EmptyBasket,
    MultipleCurrencies,
    IdempotencyConflict
}

public sealed record CreateOrderResult(
    CreateOrderStatus Status,
    Order? Order,
    string? Error,
    bool IsReplay)
{
    public static CreateOrderResult Succeeded(
        Order order,
        bool isReplay = false)
    {
        return new CreateOrderResult(
            CreateOrderStatus.Success,
            order,
            null,
            isReplay);
    }

    public static CreateOrderResult EmptyBasket()
    {
        return new CreateOrderResult(
            CreateOrderStatus.EmptyBasket,
            null,
            "The basket is empty.",
            false);
    }

    public static CreateOrderResult MultipleCurrencies()
    {
        return new CreateOrderResult(
            CreateOrderStatus.MultipleCurrencies,
            null,
            "An order cannot contain items in multiple currencies.",
            false);
    }

    public static CreateOrderResult IdempotencyConflict()
    {
        return new CreateOrderResult(
            CreateOrderStatus.IdempotencyConflict,
            null,
            "The idempotency key was already used with different checkout data.",
            false);
    }
}
