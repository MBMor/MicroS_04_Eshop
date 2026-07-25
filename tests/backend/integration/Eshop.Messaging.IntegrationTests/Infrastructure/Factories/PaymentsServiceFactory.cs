using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentsService;
using PaymentsService.Data;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Factories;

public sealed class PaymentsServiceFactory
    : EshopServiceFactory<PaymentsServiceAssemblyMarker>
{
    private readonly MessagingSystemFixture _fixture;

    public PaymentsServiceFactory(
        MessagingSystemFixture fixture,
        bool suppressHostedServices = false)
        : base(
            fixture,
            connectionStringName: "PaymentsDb",
            connectionString:
                fixture.PaymentsConnectionString,
            clientProvidedName:
                "payments-service-integration-tests",
            suppressHostedServices)
    {
        _fixture = fixture;
    }

    protected override void ConfigureAdditionalServices(
        IServiceCollection services)
    {
        services.RemoveAll<
            DbContextOptions<PaymentsDbContext>>();

        services.RemoveAll<PaymentsDbContext>();

        services.AddDbContext<PaymentsDbContext>(
            options =>
            {
                options.UseNpgsql(
                    _fixture.PaymentsConnectionString,
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(
                            typeof(PaymentsDbContext)
                                .Assembly
                                .GetName()
                                .Name);
                    });
            });
    }
}
