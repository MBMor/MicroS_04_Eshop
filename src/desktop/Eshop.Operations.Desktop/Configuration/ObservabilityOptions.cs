namespace Eshop.Operations.Desktop.Configuration;

public sealed class ObservabilityOptions
{
    public const string SectionName =
        "Observability";

    public string DashboardUrl
    {
        get;
        init;
    } = string.Empty;
}
