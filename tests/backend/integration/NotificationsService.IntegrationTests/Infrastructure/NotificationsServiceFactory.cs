using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NotificationsService.IntegrationTests.Infrastructure;

internal sealed class NotificationsServiceFactory(
    string postgresConnectionString)
    : WebApplicationFactory<NotificationsServiceAssemblyMarker>
{
    private const string TestingEnvironment =
        "Testing";

    private readonly string _postgresConnectionString =
        postgresConnectionString;

    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);

        builder.ConfigureHostConfiguration(
            configurationBuilder =>
            {
                configurationBuilder.AddInMemoryCollection(
                    CreateSettings());
            });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);

        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    CreateSettings());
            });

        builder.ConfigureTestServices(services =>
        {
            RemoveNotificationsHostedServices(services);

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
        });
    }

    private Dictionary<string, string?>
        CreateSettings()
    {
        return new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:NotificationsDb"] =
                _postgresConnectionString,

            ["Keycloak:Authority"] =
                "http://keycloak.integration.test/" +
                "realms/eshop",

            ["Keycloak:Audience"] =
                "eshop-api",

            ["Keycloak:RequireHttpsMetadata"] =
                "false",

            ["RabbitMq:HostName"] =
                "localhost",

            ["RabbitMq:Port"] =
                "5672",

            ["RabbitMq:UserName"] =
                "guest",

            ["RabbitMq:Password"] =
                "guest",

            ["RabbitMq:VirtualHost"] =
                "/",

            ["RabbitMq:ClientProvidedName"] =
                "notifications-service-integration-tests",

            ["RabbitMq:RequestedHeartbeatSeconds"] =
                "30",

            ["RabbitMq:AutomaticRecoveryEnabled"] =
                "true",

            ["RabbitMq:TopologyRecoveryEnabled"] =
                "true",

            ["RabbitMq:ConsumerDeliveryLimit"] =
                "5",

            ["Logging:LogLevel:Default"] =
                "Warning",

            ["Logging:LogLevel:Microsoft.AspNetCore"] =
                "Warning",

            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] =
                "Warning"
        };
    }

    private static void RemoveNotificationsHostedServices(
        IServiceCollection services)
    {
        ServiceDescriptor[] descriptors = services
            .Where(
                descriptor =>
                    descriptor.ServiceType ==
                    typeof(IHostedService)
                    &&
                    descriptor.ImplementationType
                        ?.Assembly ==
                    typeof(NotificationsServiceAssemblyMarker)
                        .Assembly)
            .ToArray();

        foreach (ServiceDescriptor descriptor
                 in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}
