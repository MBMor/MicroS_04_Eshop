namespace OrdersService.Options;

public sealed class OrdersOptions
{
    public const string SectionName = "Orders";

    public bool AllowDevelopmentCustomerHeader { get; init; } = true;

    public string DevelopmentCustomerHeaderName { get; init; }
        = "X-Customer-Id";
}
