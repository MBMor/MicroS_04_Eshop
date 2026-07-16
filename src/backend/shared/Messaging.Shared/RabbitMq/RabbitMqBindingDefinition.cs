namespace Messaging.Shared.RabbitMq;

public sealed record RabbitMqBindingDefinition(
    string QueueName,
    string RoutingKey);
