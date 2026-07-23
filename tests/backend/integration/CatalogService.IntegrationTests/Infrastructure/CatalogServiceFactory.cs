using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CatalogService.IntegrationTests.Infrastructure;

internal sealed class CatalogServiceFactory(
    string postgresConnectionString)
    : WebApplicationFactory<CatalogServiceAssemblyMarker>
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
    }

    private Dictionary<string, string?>
        CreateSettings()
    {
        return new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:CatalogDb"] =
                _postgresConnectionString,

            ["Logging:LogLevel:Default"] =
                "Warning",

            ["Logging:LogLevel:Microsoft.AspNetCore"] =
                "Warning",

            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] =
                "Warning"
        };
    }
}
