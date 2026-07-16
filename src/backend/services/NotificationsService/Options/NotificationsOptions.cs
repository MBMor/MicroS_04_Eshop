namespace NotificationsService.Options;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public bool AllowDevelopmentCustomerHeader { get; init; } = true;

    public string DevelopmentCustomerHeaderName { get; init; }
        = "X-Customer-Id";
}
