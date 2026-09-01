using System.Security.Claims;
using ApiGateway.OperationalHealth;
using ApiGateway.RateLimiting;
using Eshop.ErrorHandling;
using Eshop.HealthChecks;
using Eshop.Observability;
using Eshop.Security.Authentication;
using Eshop.Security.Authorization;

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddEshopObservability(
    builder.Configuration,
    serviceName: "api-gateway");

builder.Services.AddHealthChecks();

builder.Services.AddHttpClient(
    "OperationalHealthProbe",
    client =>
    {
        client.Timeout =
            Timeout.InfiniteTimeSpan;
    });

builder.Services.AddSingleton<
    OperationalHealthService>();

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopJwtAuthentication(
    builder.Configuration);

builder.Services.AddEshopAuthorization();

builder.Services.AddGatewayRateLimiting(
    builder.Configuration);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection(
            "ReverseProxy"));

WebApplication app = builder.Build();

app.UseEshopErrorHandling();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
{
    Service = "ApiGateway",
    Status = "Running"
}));

app.MapEshopHealthChecks();

app.MapGet(
        "/api/v1/operations/health",
        async (
            OperationalHealthService healthService,
            CancellationToken cancellationToken) =>
        {
            OperationalHealthResponse response =
                await healthService.CheckAsync(
                    cancellationToken);

            return Results.Ok(response);
        })
    .RequireAuthorization(
        EshopPolicies.SupportOrAdmin)
    .RequireRateLimiting(
        "Operational");

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

static string? FindClaim(
    ClaimsPrincipal user,
    string claimType)
{
    return user.FindFirst(claimType)?.Value;
}
