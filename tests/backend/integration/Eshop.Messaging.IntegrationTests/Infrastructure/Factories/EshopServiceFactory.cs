using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;
using Microsoft.AspNetCore.Authentication;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Factories;

public abstract class EshopServiceFactory<TEntryPoint>(
    MessagingSystemFixture fixture,
    string connectionStringName,
    string connectionString,
    string clientProvidedName,
    bool suppressHostedServices = false)
    : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly MessagingSystemFixture _fixture =
        fixture;

    private readonly string _connectionStringName =
        connectionStringName;

    private readonly string _connectionString =
        connectionString;

    private readonly string _clientProvidedName =
        clientProvidedName;

    private readonly bool _suppressHostedServices =
        suppressHostedServices;

    protected override IHost CreateHost(
    IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Dictionary<string, string?> settings =
            CreateConfiguredSettings();

        builder.ConfigureHostConfiguration(
            configurationBuilder =>
            {
                configurationBuilder.AddInMemoryCollection(
                    settings);
            });

        return base.CreateHost(builder);
    }

    protected sealed override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    CreateConfiguredSettings());
            });

        builder.ConfigureTestServices(
            services =>
            {
                if (_suppressHostedServices)
                {
                    services.RemoveAll<IHostedService>();
                }

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme =
                            TestAuthenticationHandler.SchemeName;

                        options.DefaultChallengeScheme =
                            TestAuthenticationHandler.SchemeName;

                        options.DefaultForbidScheme =
                            TestAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ =>
                        {
                        });

                ConfigureAdditionalServices(services);
            });
    }

    protected virtual void AddServiceSettings(
        Dictionary<string, string?> settings)
    {
    }

    protected virtual void ConfigureAdditionalServices(
        IServiceCollection services)
    {
    }

    private Dictionary<string, string?>
    CreateConfiguredSettings()
    {
        Dictionary<string, string?> settings =
            CreateSettings();

        AddServiceSettings(settings);

        return settings;
    }

    private Dictionary<string, string?> CreateSettings()
    {
        return new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase)
        {
            [$"ConnectionStrings:{_connectionStringName}"] =
                _connectionString,

            ["RabbitMq:HostName"] =
                _fixture.RabbitMqHostName,

            ["RabbitMq:Port"] =
                _fixture.RabbitMqPort.ToString(
                    CultureInfo.InvariantCulture),

            ["RabbitMq:UserName"] =
                MessagingSystemFixture.RabbitMqUserName,

            ["RabbitMq:Password"] =
                MessagingSystemFixture.RabbitMqPassword,

            ["RabbitMq:VirtualHost"] =
                MessagingSystemFixture.RabbitMqVirtualHost,

            ["RabbitMq:ClientProvidedName"] =
                _clientProvidedName,

            ["RabbitMq:RequestedHeartbeatSeconds"] =
                "10",

            ["RabbitMq:AutomaticRecoveryEnabled"] =
                "true",

            ["RabbitMq:TopologyRecoveryEnabled"] =
                "true",

            ["RabbitMq:ConsumerDeliveryLimit"] =
                "3",

            ["RabbitMq:NetworkRecoveryIntervalSeconds"] =
                "1",

            ["RabbitMq:RequestedConnectionTimeoutSeconds"] =
                "2",

            ["RabbitMq:PublishTimeoutSeconds"] =
                "3",

            ["Outbox:BatchSize"] =
                "10",

            ["Outbox:PollingInterval"] =
                "00:00:00.100",

            ["Outbox:ClaimTimeout"] =
                "00:00:30",

            ["Outbox:MaximumRetryCount"] =
                "50",

            ["Outbox:InitialRetryDelay"] =
                "00:00:00.100",

            ["Outbox:MaximumRetryDelay"] =
                "00:00:01",

            ["Outbox:PublishedRetention"] =
                "7.00:00:00",

            ["Outbox:CleanupInterval"] =
                "01:00:00",

            ["Outbox:CleanupBatchSize"] =
                "100",

            ["OTEL_EXPORTER_OTLP_ENDPOINT"] =
                string.Empty,

            ["Logging:LogLevel:Default"] =
                "Warning",

            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] =
                "Warning",

            ["Keycloak:Authority"] =
                "http://keycloak.integration.test/realms/eshop",

            ["Keycloak:Audience"] =
                "eshop-api",

            ["Keycloak:RequireHttpsMetadata"] =
                "false"
        };
    }
}
