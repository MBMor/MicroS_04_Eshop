using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotificationsService;
using NotificationsService.Data;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Factories;

public sealed class NotificationsServiceFactory(
    MessagingSystemFixture fixture,
    bool suppressHostedServices = false)
    : EshopServiceFactory<
        NotificationsServiceAssemblyMarker>(
            fixture,
            connectionStringName: "NotificationsDb",
            connectionString:
                fixture.NotificationsConnectionString,
            clientProvidedName:
                "notifications-service-integration-tests",
            suppressHostedServices)
{
    protected override void AddServiceSettings(
        Dictionary<string, string?> settings)
    {
        settings[
            "Notifications:AllowDevelopmentCustomerHeader"] =
                "true";

        settings[
            "Notifications:DevelopmentCustomerHeaderName"] =
                "X-Customer-Id";
    }

    protected override void ConfigureAdditionalServices(
        IServiceCollection services)
    {
        services.RemoveAll<
            DbContextOptions<NotificationsDbContext>>();

        services.RemoveAll<NotificationsDbContext>();

        services.AddDbContext<NotificationsDbContext>(
            options =>
            {
                options.UseNpgsql(
                    fixture.NotificationsConnectionString,
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(
                            typeof(NotificationsDbContext)
                                .Assembly
                                .GetName()
                                .Name);
                    });
            });
    }
}
