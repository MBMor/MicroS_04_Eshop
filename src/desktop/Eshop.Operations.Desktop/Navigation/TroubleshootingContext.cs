namespace Eshop.Operations.Desktop.Navigation;

public enum TroubleshootingContextKind
{
    OrderToPayments,
    ProductToInventory,
    PaymentToOrder,
    LookupToOrder,
    OrderToNotifications,
    NotificationToOrder
}

public sealed record TroubleshootingContext(
    TroubleshootingContextKind Kind,
    Guid CorrelationId)
{
    public string DisplayText =>
        Kind switch
        {
            TroubleshootingContextKind.OrderToPayments =>
                $"Order {ShortCorrelationId} → Payments",
            TroubleshootingContextKind.ProductToInventory =>
                $"Product {ShortCorrelationId} → Inventory",
            TroubleshootingContextKind.PaymentToOrder =>
                $"Payments → Order {ShortCorrelationId}",
            TroubleshootingContextKind.LookupToOrder =>
                $"Lookup → Order {ShortCorrelationId}",
            TroubleshootingContextKind.OrderToNotifications =>
                $"Order {ShortCorrelationId} → Notifications",
            TroubleshootingContextKind.NotificationToOrder =>
                $"Notification → Order {ShortCorrelationId}",
            _ => ShortCorrelationId
        };

    public string CorrelationText => CorrelationId.ToString("D");

    private string ShortCorrelationId =>
        $"{CorrelationText[..8]}…";
}
