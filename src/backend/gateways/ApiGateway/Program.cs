using System.Security.Claims;
using ErrorHandling.Shared;
using Eshop.Security.Authentication;
using Eshop.Security.Authorization;

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopJwtAuthentication(
    builder.Configuration);

builder.Services.AddEshopAuthorization();

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

static string? FindClaim(
    ClaimsPrincipal user,
    string claimType)
{
    return user.FindFirst(claimType)?.Value;
}
