using OrdersService.Domain;
using Xunit;

namespace Eshop.Domain.UnitTests.Orders;

public sealed class OrderTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            year: 2026,
            month: 7,
            day: 23,
            hour: 8,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

    [Fact]
    public void Create_ValidItems_CreatesPendingOrderAndInitialHistory()
    {
        Guid orderId = Guid.NewGuid();

        Order order = CreateOrder(orderId);

        Assert.Equal(orderId, order.Id);
        Assert.Equal("customer-1", order.CustomerId);
        Assert.Equal("alice@example.com", order.CustomerEmail);
        Assert.Equal("test-success", order.PaymentMethod);

        Assert.Equal(
            OrderStatus.PendingStockReservation,
            order.Status);

        Assert.Equal(250m, order.TotalAmount);
        Assert.Equal("CZK", order.Currency);
        Assert.Equal(CreatedAtUtc, order.CreatedAtUtc);
        Assert.Null(order.UpdatedAtUtc);
        Assert.Equal(2, order.Items.Count);

        OrderStatusHistory initialHistory =
            Assert.Single(order.StatusHistory);

        Assert.Null(initialHistory.FromStatus);

        Assert.Equal(
            OrderStatus.PendingStockReservation,
            initialHistory.ToStatus);

        Assert.Equal(
            "Order created and awaiting stock reservation.",
            initialHistory.Reason);

        Assert.Equal(
            CreatedAtUtc,
            initialHistory.ChangedAtUtc);
    }

    [Fact]
    public void Create_ItemsUsingDifferentCurrencies_Throws()
    {
        Guid orderId = Guid.NewGuid();

        OrderItem[] items =
        [
            CreateItem(
                orderId,
                productName: "Keyboard",
                unitPrice: 100m,
                currency: "CZK",
                quantity: 1),

            CreateItem(
                orderId,
                productName: "Mouse",
                unitPrice: 50m,
                currency: "EUR",
                quantity: 1)
        ];

        Assert.Throws<ArgumentException>(
            () => Order.Create(
                orderId,
                "customer-1",
                "alice@example.com",
                "test-success",
                items,
                CreatedAtUtc));
    }

    [Fact]
    public void Create_ItemBelongingToDifferentOrder_Throws()
    {
        Guid orderId = Guid.NewGuid();

        OrderItem[] items =
        [
            CreateItem(
                orderId,
                productName: "Keyboard",
                unitPrice: 100m,
                currency: "CZK",
                quantity: 1),

            CreateItem(
                Guid.NewGuid(),
                productName: "Mouse",
                unitPrice: 50m,
                currency: "CZK",
                quantity: 1)
        ];

        Assert.Throws<ArgumentException>(
            () => Order.Create(
                orderId,
                "customer-1",
                "alice@example.com",
                "test-success",
                items,
                CreatedAtUtc));
    }

    [Fact]
    public void MarkStockReserved_PendingOrder_MovesToPendingPayment()
    {
        Order order = CreateOrder();
        DateTimeOffset updatedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        order.MarkStockReserved(updatedAtUtc);

        Assert.Equal(
            OrderStatus.PendingPayment,
            order.Status);

        Assert.Equal(updatedAtUtc, order.UpdatedAtUtc);
        Assert.Equal(2, order.StatusHistory.Count);

        OrderStatusHistory history =
            order.StatusHistory.Last();

        Assert.Equal(
            OrderStatus.PendingStockReservation,
            history.FromStatus);

        Assert.Equal(
            OrderStatus.PendingPayment,
            history.ToStatus);

        Assert.Equal(
            "Stock reserved; order is awaiting payment.",
            history.Reason);

        Assert.Equal(updatedAtUtc, history.ChangedAtUtc);
    }

    [Fact]
    public void MarkStockReservationFailed_PendingOrder_StoresNormalizedReason()
    {
        Order order = CreateOrder();
        DateTimeOffset updatedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        order.MarkStockReservationFailed(
            "  Insufficient stock.  ",
            updatedAtUtc);

        Assert.Equal(
            OrderStatus.StockReservationFailed,
            order.Status);

        OrderStatusHistory history =
            order.StatusHistory.Last();

        Assert.Equal(
            OrderStatus.PendingStockReservation,
            history.FromStatus);

        Assert.Equal(
            OrderStatus.StockReservationFailed,
            history.ToStatus);

        Assert.Equal(
            "Insufficient stock.",
            history.Reason);
    }

    [Fact]
    public void MarkPaymentAuthorized_PendingPayment_ConfirmsOrder()
    {
        Order order = CreateOrder();

        order.MarkStockReserved(
            CreatedAtUtc.AddMinutes(1));

        DateTimeOffset authorizedAtUtc =
            CreatedAtUtc.AddMinutes(2);

        order.MarkPaymentAuthorized(
            authorizedAtUtc);

        Assert.Equal(
            OrderStatus.Confirmed,
            order.Status);

        Assert.Equal(
            authorizedAtUtc,
            order.UpdatedAtUtc);

        Assert.Equal(3, order.StatusHistory.Count);

        OrderStatusHistory history =
            order.StatusHistory.Last();

        Assert.Equal(
            OrderStatus.PendingPayment,
            history.FromStatus);

        Assert.Equal(
            OrderStatus.Confirmed,
            history.ToStatus);

        Assert.Equal(
            "Payment authorized.",
            history.Reason);
    }

    [Fact]
    public void PaymentFailureThenCancellation_RecordsBothTransitions()
    {
        Order order = CreateOrder();

        order.MarkStockReserved(
            CreatedAtUtc.AddMinutes(1));

        order.MarkPaymentFailed(
            "  Payment declined.  ",
            CreatedAtUtc.AddMinutes(2));

        order.Cancel(
            "  Stock reservation released.  ",
            CreatedAtUtc.AddMinutes(3));

        Assert.Equal(
            OrderStatus.Cancelled,
            order.Status);

        Assert.Equal(4, order.StatusHistory.Count);

        OrderStatusHistory[] histories =
            order.StatusHistory.ToArray();

        Assert.Equal(
            OrderStatus.PaymentFailed,
            histories[^2].ToStatus);

        Assert.Equal(
            "Payment declined.",
            histories[^2].Reason);

        Assert.Equal(
            OrderStatus.PaymentFailed,
            histories[^1].FromStatus);

        Assert.Equal(
            OrderStatus.Cancelled,
            histories[^1].ToStatus);

        Assert.Equal(
            "Stock reservation released.",
            histories[^1].Reason);
    }

    [Fact]
    public void MarkPaymentAuthorized_BeforeStockReservation_ThrowsWithoutMutation()
    {
        Order order = CreateOrder();

        Assert.Throws<InvalidOperationException>(
            () => order.MarkPaymentAuthorized(
                CreatedAtUtc.AddMinutes(1)));

        Assert.Equal(
            OrderStatus.PendingStockReservation,
            order.Status);

        Assert.Null(order.UpdatedAtUtc);
        Assert.Single(order.StatusHistory);
    }

    [Fact]
    public void Cancel_ConfirmedOrder_ThrowsWithoutMutation()
    {
        Order order = CreateOrder();

        order.MarkStockReserved(
            CreatedAtUtc.AddMinutes(1));

        order.MarkPaymentAuthorized(
            CreatedAtUtc.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(
            () => order.Cancel(
                "Cannot cancel confirmed order.",
                CreatedAtUtc.AddMinutes(3)));

        Assert.Equal(
            OrderStatus.Confirmed,
            order.Status);

        Assert.Equal(3, order.StatusHistory.Count);
    }

    private static Order CreateOrder(
        Guid? orderId = null)
    {
        Guid id = orderId ?? Guid.NewGuid();

        return Order.Create(
            id,
            "  customer-1  ",
            "  alice@example.com  ",
            "  test-success  ",
            CreateItems(id),
            CreatedAtUtc);
    }

    private static OrderItem[] CreateItems(
        Guid orderId)
    {
        return
        [
            CreateItem(
                orderId,
                productName: "Keyboard",
                unitPrice: 100m,
                currency: "czk",
                quantity: 2),

            CreateItem(
                orderId,
                productName: "Mouse",
                unitPrice: 50m,
                currency: "CZK",
                quantity: 1)
        ];
    }

    private static OrderItem CreateItem(
        Guid orderId,
        string productName,
        decimal unitPrice,
        string currency,
        int quantity)
    {
        return OrderItem.Create(
            Guid.NewGuid(),
            orderId,
            Guid.NewGuid(),
            productName,
            unitPrice,
            currency,
            quantity);
    }
}
