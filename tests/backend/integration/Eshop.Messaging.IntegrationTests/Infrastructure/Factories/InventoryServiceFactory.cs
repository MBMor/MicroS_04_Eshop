using InventoryService;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Factories;

public sealed class InventoryServiceFactory(
    MessagingSystemFixture fixture,
    bool suppressHostedServices = false)
    : EshopServiceFactory<
        InventoryServiceAssemblyMarker>(
            fixture,
            connectionStringName: "InventoryDb",
            connectionString:
                fixture.InventoryConnectionString,
            clientProvidedName:
                "inventory-service-integration-tests",
            suppressHostedServices)
{
    protected override void ConfigureAdditionalServices(
        IServiceCollection services)
    {
        services.RemoveAll<
            DbContextOptions<InventoryDbContext>>();

        services.RemoveAll<InventoryDbContext>();

        services.AddDbContext<InventoryDbContext>(
            options =>
            {
                options.UseNpgsql(
                    fixture.InventoryConnectionString,
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(
                            typeof(InventoryDbContext)
                                .Assembly
                                .GetName()
                                .Name);
                    });
            });
    }
}
