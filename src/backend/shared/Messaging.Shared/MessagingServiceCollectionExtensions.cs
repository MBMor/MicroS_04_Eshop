using Messaging.Shared.RabbitMq;
using Messaging.Shared.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Messaging.Shared.Abstractions;

namespace Messaging.Shared;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddEshopMessagingCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(
                configuration.GetSection(
                    RabbitMqOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.HostName),
                "RabbitMQ host name must be configured.")
            .Validate(
                options =>
                    options.Port is > 0 and <= 65_535,
                "RabbitMQ port must be between 1 and 65535.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.UserName),
                "RabbitMQ user name must be configured.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.Password),
                "RabbitMQ password must be configured.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.VirtualHost),
                "RabbitMQ virtual host must be configured.")
            .Validate(
                options =>
                    options.ConsumerDeliveryLimit is >= 1 and <= 20,
                "RabbitMQ consumer delivery limit must be between 1 and 20.")
            .ValidateOnStart();

        services.AddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>();
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();

        return services;
    }
}
