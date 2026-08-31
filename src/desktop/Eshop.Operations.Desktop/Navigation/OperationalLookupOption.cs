namespace Eshop.Operations.Desktop.Navigation;

public enum OperationalLookupKind
{
    Order,
    PaymentsForOrder,
    InventoryForProduct
}

public sealed record OperationalLookupOption(
    string Title,
    OperationalLookupKind Kind);
