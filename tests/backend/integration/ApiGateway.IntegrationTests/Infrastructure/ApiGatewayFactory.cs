using ApiGateway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApiGateway.IntegrationTests.Infrastructure;

internal sealed class ApiGatewayFactory(
    Uri downstreamAddress)
    : WebApplicationFactory<ApiGatewayAssemblyMarker>
{
    private const string TestingEnvironment = "Testing";

    private readonly Uri _downstreamAddress =
        downstreamAddress;

    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureHostConfiguration(
            configurationBuilder =>
            {
                configurationBuilder
                    .AddInMemoryCollection(
                        CreateSettings());
            });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment(
            TestingEnvironment);

        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder
                    .AddInMemoryCollection(
                        CreateSettings());
            });

        builder.ConfigureTestServices(
            services =>
            {
                services
                    .AddAuthentication(
                        authenticationOptions =>
                        {
                            authenticationOptions
                                .DefaultAuthenticateScheme =
                                TestAuthenticationHandler
                                    .SchemeName;

                            authenticationOptions
                                .DefaultChallengeScheme =
                                TestAuthenticationHandler
                                    .SchemeName;

                            authenticationOptions
                                .DefaultForbidScheme =
                                TestAuthenticationHandler
                                    .SchemeName;
                        })
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        TestAuthenticationHandler>(
                        TestAuthenticationHandler
                            .SchemeName,
                        _ =>
                        {
                        });
            });
    }

    private Dictionary<string, string?>
        CreateSettings()
    {
        string downstreamAddress =
            _downstreamAddress.AbsoluteUri;

        return new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Keycloak:Authority"] =
                "http://keycloak.integration.test/realms/eshop",

            ["Keycloak:Audience"] =
                "eshop-api",

            ["Keycloak:RequireHttpsMetadata"] =
                "false",

            ["ReverseProxy:Clusters:" +
             "catalog-cluster:Destinations:" +
             "catalog-service:Address"] =
                downstreamAddress,

            ["ReverseProxy:Clusters:" +
             "basket-cluster:Destinations:" +
             "basket-service:Address"] =
                downstreamAddress,

            ["ReverseProxy:Clusters:" +
             "orders-cluster:Destinations:" +
             "orders-service:Address"] =
                downstreamAddress,

            ["ReverseProxy:Clusters:" +
             "inventory-cluster:Destinations:" +
             "inventory-service:Address"] =
                downstreamAddress,

            ["ReverseProxy:Clusters:" +
             "payments-cluster:Destinations:" +
             "payments-service:Address"] =
                downstreamAddress,

            ["ReverseProxy:Clusters:" +
             "notifications-cluster:Destinations:" +
             "notifications-service:Address"] =
                downstreamAddress,

            ["Logging:LogLevel:Default"] =
                "Warning",

            ["Logging:LogLevel:" +
             "Microsoft.AspNetCore"] =
                "Warning",

            ["Logging:LogLevel:" +
             "Yarp.ReverseProxy"] =
                "Warning"
        };
    }
}
