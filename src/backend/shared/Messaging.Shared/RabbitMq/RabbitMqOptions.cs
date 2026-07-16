namespace Messaging.Shared.RabbitMq;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "eshop";

    public string Password { get; set; } = "eshop_password";

    public string VirtualHost { get; set; } = "/";

    public string ClientProvidedName { get; set; } = "eshop-service";

    public ushort RequestedHeartbeatSeconds { get; set; } = 30;

    public bool AutomaticRecoveryEnabled { get; set; } = true;

    public bool TopologyRecoveryEnabled { get; set; } = true;
}
