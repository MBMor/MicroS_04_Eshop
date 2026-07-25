namespace Eshop.Messaging.RabbitMq;

public sealed record RabbitMqBindingDefinition(
    string QueueName,
    string RoutingKey);
