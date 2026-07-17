namespace Messaging.Shared.RabbitMq;

public static class RabbitMqTopologySettings
{
    public const int DeliveryLimit = 3;
    public const string QueueType = "quorum";
}
