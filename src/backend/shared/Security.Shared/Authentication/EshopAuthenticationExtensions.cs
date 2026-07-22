using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Eshop.Security.Authentication;

public static class EshopAuthenticationExtensions
{
    public static IServiceCollection AddEshopJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection keycloakSection =
            configuration.GetRequiredSection(KeycloakOptions.SectionName);

        string authority = GetRequiredValue(
                keycloakSection,
                nameof(KeycloakOptions.Authority))
            .TrimEnd('/');

        string audience = GetRequiredValue(
            keycloakSection,
            nameof(KeycloakOptions.Audience));

        bool requireHttpsMetadata = keycloakSection.GetValue(
            nameof(KeycloakOptions.RequireHttpsMetadata),
            defaultValue: true);

        ValidateAuthority(
            authority,
            requireHttpsMetadata);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.MapInboundClaims = false;

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authority,

                        ValidateAudience = true,
                        ValidAudience = audience,

                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        NameClaimType =
                            EshopClaimNames.PreferredUsername,

                        RoleClaimType =
                            EshopClaimNames.Roles,

                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
            });

        return services;
    }

    private static string GetRequiredValue(
        IConfigurationSection section,
        string key)
    {
        string? value = section[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Configuration value '{section.Path}:{key}' is required.");
        }

        return value;
    }

    private static void ValidateAuthority(
        string authority,
        bool requireHttpsMetadata)
    {
        if (!Uri.TryCreate(
                authority,
                UriKind.Absolute,
                out Uri? authorityUri))
        {
            throw new InvalidOperationException(
                "Keycloak:Authority must be a valid absolute URI.");
        }

        if (requireHttpsMetadata &&
            !string.Equals(
                authorityUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Keycloak:Authority must use HTTPS when " +
                "Keycloak:RequireHttpsMetadata is enabled.");
        }
    }
}
