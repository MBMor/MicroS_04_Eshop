using OrdersService.Domain;

namespace OrdersService.Application;

public enum CreateOrderStatus
{
    Success,
    EmptyBasket,
    MultipleCurrencies
}

public sealed record CreateOrderResult(
    CreateOrderStatus Status,
    Order? Order,
    string? Error)
{
    public static CreateOrderResult Succeeded(Order order)
    {
        return new CreateOrderResult(
            CreateOrderStatus.Success,
            order,
            null);
    }

    public static CreateOrderResult EmptyBasket()
    {
        return new CreateOrderResult(
            CreateOrderStatus.EmptyBasket,
            null,
            "The basket is empty.");
    }

    public static CreateOrderResult MultipleCurrencies()
    {
        return new CreateOrderResult(
            CreateOrderStatus.MultipleCurrencies,
            null,
            "An order cannot contain items in multiple currencies.");
    }
}
