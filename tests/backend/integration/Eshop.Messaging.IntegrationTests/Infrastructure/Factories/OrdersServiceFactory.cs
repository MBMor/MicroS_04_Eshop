using Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrdersService;
using OrdersService.Application;
using OrdersService.Data;
using OrdersService.Identity;
using OrdersService.Integration;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Factories;

public sealed class OrdersServiceFactory(
    MessagingSystemFixture fixture,
    bool suppressHostedServices = false)
    : EshopServiceFactory<OrdersServiceAssemblyMarker>(
        fixture,
        connectionStringName: "OrdersDb",
        connectionString:
            fixture.OrdersConnectionString,
        clientProvidedName:
            "orders-service-integration-tests",
        suppressHostedServices)
{
    public TestBasketClient BasketClient { get; } =
        new();

    public TransientConsumerFailureState TransientConsumerFailures { get; } =
        new();

    protected override void AddServiceSettings(
        Dictionary<string, string?> settings)
    {
        settings["Services:BasketBaseUrl"] =
            "http://127.0.0.1";

        settings[
            "Orders:AllowDevelopmentCustomerHeader"] =
                "true";

        settings[
            "Orders:DevelopmentCustomerHeaderName"] =
                "X-Customer-Id";
    }

    protected override void ConfigureAdditionalServices(
        IServiceCollection services)
    {
        ReplaceDbContext(services);

        services.RemoveAll<IBasketClient>();

        services.AddSingleton<IBasketClient>(
            BasketClient);

        services.RemoveAll<IOrderOwnerProvider>();

        services.AddSingleton<
            IOrderOwnerProvider,
            TestOrderOwnerProvider>();

        services.RemoveAll<IOrderStockResultService>();

        services.AddSingleton(
            TransientConsumerFailures);

        services.AddScoped<
            IOrderStockResultService,
            TransientFailureOrderStockResultService>();
    }

    private void ReplaceDbContext(
        IServiceCollection services)
    {
        services.RemoveAll<
            DbContextOptions<OrdersDbContext>>();

        services.RemoveAll<OrdersDbContext>();

        services.AddDbContext<OrdersDbContext>(
            options =>
            {
                options.UseNpgsql(
                    fixture.OrdersConnectionString,
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(
                            typeof(OrdersDbContext)
                                .Assembly
                                .GetName()
                                .Name);
                    });
            });
    }
}
