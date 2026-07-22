using Microsoft.Extensions.DependencyInjection;

namespace Eshop.Security.Authorization;

public static class EshopAuthorizationExtensions
{
    public static IServiceCollection AddEshopAuthorization(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                EshopPolicies.AuthenticatedUser,
                policy => policy.RequireAuthenticatedUser())
            .AddPolicy(
                EshopPolicies.CustomerOnly,
                policy => policy.RequireRole(
                    EshopRoles.Customer))
            .AddPolicy(
                EshopPolicies.SupportOnly,
                policy => policy.RequireRole(
                    EshopRoles.Support))
            .AddPolicy(
                EshopPolicies.AdminOnly,
                policy => policy.RequireRole(
                    EshopRoles.Admin))
            .AddPolicy(
                EshopPolicies.SupportOrAdmin,
                policy => policy.RequireRole(
                    EshopRoles.Support,
                    EshopRoles.Admin))
            .AddPolicy(
                EshopPolicies.CustomerOrAdmin,
                policy => policy.RequireRole(
                    EshopRoles.Customer,
                    EshopRoles.Admin));

        return services;
    }
}
