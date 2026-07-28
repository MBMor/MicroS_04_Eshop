using InventoryService;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Factories;

public sealed class InventoryServiceFactory
    : EshopServiceFactory<InventoryServiceAssemblyMarker>
{
    private readonly MessagingSystemFixture _fixture;

    private readonly SaveChangesInterceptor?
        _saveChangesInterceptor;

    public InventoryServiceFactory(
        MessagingSystemFixture fixture,
        bool suppressHostedServices = false,
        SaveChangesInterceptor? saveChangesInterceptor = null,
        string clientProvidedName =
            "inventory-service-integration-tests")
        : base(
            fixture,
            connectionStringName: "InventoryDb",
            connectionString:
                fixture.InventoryConnectionString,
            clientProvidedName,
            suppressHostedServices)
    {
        _fixture = fixture;
        _saveChangesInterceptor =
            saveChangesInterceptor;
    }

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
                    _fixture.InventoryConnectionString,
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(
                            typeof(InventoryDbContext)
                                .Assembly
                                .GetName()
                                .Name);
                    });

                if (_saveChangesInterceptor is not null)
                {
                    options.AddInterceptors(
                        _saveChangesInterceptor);
                }
            });
    }
}
