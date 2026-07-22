using System.Security.Claims;
using ErrorHandling.Shared;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IConfigurationSection keycloakSection =
    builder.Configuration.GetRequiredSection("Keycloak");

string authority = GetRequiredConfigurationValue(
        keycloakSection,
        "Authority")
    .TrimEnd('/');

string audience = GetRequiredConfigurationValue(
    keycloakSection,
    "Audience");

bool requireHttpsMetadata = keycloakSection.GetValue(
    "RequireHttpsMetadata",
    defaultValue: true);

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

builder.Services.AddHealthChecks();

builder.Services.AddEshopErrorHandling();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = requireHttpsMetadata;

        // Keep original Keycloak claim names such as:
        // sub, preferred_username and roles.
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        EshopPolicies.AuthenticatedUser,
        policy => policy.RequireAuthenticatedUser());
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"));

WebApplication app = builder.Build();

app.UseEshopErrorHandling();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    Service = "ApiGateway",
    Status = "Running"
}));

app.MapHealthChecks("/health");

app.MapGet(
        "/api/v1/auth/me",
        (ClaimsPrincipal user) =>
        {
            string[] roles = user
                .FindAll(EshopClaimNames.Roles)
                .Select(static claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            return Results.Ok(new
            {
                Subject = FindClaim(
                    user,
                    EshopClaimNames.Subject),

                PreferredUsername = FindClaim(
                    user,
                    EshopClaimNames.PreferredUsername),

                Email = FindClaim(
                    user,
                    EshopClaimNames.Email),

                Roles = roles
            });
        })
    .RequireAuthorization(
        EshopPolicies.AuthenticatedUser);

app.MapReverseProxy();

app.Run();

static string GetRequiredConfigurationValue(
    IConfigurationSection section,
    string key)
{
    string? value = section[key];

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Configuration value " +
            $"'{section.Path}:{key}' is required.");
    }

    return value;
}

static string? FindClaim(
    ClaimsPrincipal user,
    string claimType)
{
    return user.FindFirst(claimType)?.Value;
}
