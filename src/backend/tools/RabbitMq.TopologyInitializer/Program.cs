using Messaging.Shared.RabbitMq;
using RabbitMQ.Client;

RabbitMqOptions options = LoadOptions();

ConnectionFactory connectionFactory = new()
{
    HostName = options.HostName,
    Port = options.Port,
    UserName = options.UserName,
    Password = options.Password,
    VirtualHost = options.VirtualHost,
    AutomaticRecoveryEnabled = options.AutomaticRecoveryEnabled,
    TopologyRecoveryEnabled = options.TopologyRecoveryEnabled,
    RequestedHeartbeat = TimeSpan.FromSeconds(
        options.RequestedHeartbeatSeconds)
};

Console.WriteLine(
    $"Connecting to RabbitMQ at {options.HostName}:{options.Port}...");

await using IConnection connection =
    await connectionFactory.CreateConnectionAsync(
        options.ClientProvidedName);

await using IChannel channel =
    await connection.CreateChannelAsync();

await channel.ExchangeDeclareAsync(
    exchange: RabbitMqExchanges.Events,
    type: ExchangeType.Topic,
    durable: true,
    autoDelete: false,
    arguments: null);

await channel.ExchangeDeclareAsync(
    exchange: RabbitMqExchanges.DeadLetter,
    type: ExchangeType.Direct,
    durable: true,
    autoDelete: false,
    arguments: null);

foreach (RabbitMqBindingDefinition binding
         in RabbitMqTopology.Bindings)
{
    string deadLetterQueueName =
        RabbitMqQueues.DeadLetter(
            binding.QueueName);

    Dictionary<string, object?> queueArguments = new()
    {
        ["x-dead-letter-exchange"] =
            RabbitMqExchanges.DeadLetter,

        ["x-dead-letter-routing-key"] =
            deadLetterQueueName
    };

    await channel.QueueDeclareAsync(
        queue: binding.QueueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: queueArguments);

    await channel.QueueBindAsync(
        queue: binding.QueueName,
        exchange: RabbitMqExchanges.Events,
        routingKey: binding.RoutingKey,
        arguments: null);

    await channel.QueueDeclareAsync(
        queue: deadLetterQueueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null);

    await channel.QueueBindAsync(
        queue: deadLetterQueueName,
        exchange: RabbitMqExchanges.DeadLetter,
        routingKey: deadLetterQueueName,
        arguments: null);

    Console.WriteLine(
        $"Declared queue '{binding.QueueName}' " +
        $"for routing key '{binding.RoutingKey}'.");

    Console.WriteLine(
        $"Declared dead-letter queue " +
        $"'{deadLetterQueueName}'.");
}

Console.WriteLine(
    "RabbitMQ topology initialized successfully.");

return;

static RabbitMqOptions LoadOptions()
{
    return new RabbitMqOptions
    {
        HostName =
            GetEnvironmentValue(
                "RABBITMQ_HOST",
                "localhost"),

        Port =
            GetEnvironmentInt32(
                "RABBITMQ_PORT",
                5672),

        UserName =
            GetEnvironmentValue(
                "RABBITMQ_USERNAME",
                "eshop"),

        Password =
            GetEnvironmentValue(
                "RABBITMQ_PASSWORD",
                "eshop_password"),

        VirtualHost =
            GetEnvironmentValue(
                "RABBITMQ_VIRTUAL_HOST",
                "/"),

        ClientProvidedName =
            GetEnvironmentValue(
                "RABBITMQ_CLIENT_NAME",
                "eshop-topology-initializer"),

        RequestedHeartbeatSeconds = 30,
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true
    };
}

static string GetEnvironmentValue(
    string name,
    string defaultValue)
{
    string? value =
        Environment.GetEnvironmentVariable(name);

    return string.IsNullOrWhiteSpace(value)
        ? defaultValue
        : value.Trim();
}

static int GetEnvironmentInt32(
    string name,
    int defaultValue)
{
    string? value =
        Environment.GetEnvironmentVariable(name);

    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    return int.TryParse(value, out int result)
        && result is > 0 and <= 65_535
            ? result
            : throw new InvalidOperationException(
                $"Environment variable '{name}' does not contain a valid port.");
}
