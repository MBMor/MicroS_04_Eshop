using Asp.Versioning;
using CatalogService.Data;
using Eshop.ErrorHandling;
using Eshop.HealthChecks;
using Eshop.Security.Authentication;
using Eshop.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Eshop.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";

        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopOpenApi(
    title: "Eshop Catalog API",
    description:
        "Product catalog management and product query API.");

builder.Services.AddEshopJwtAuthentication(
    builder.Configuration);

builder.Services.AddEshopAuthorization();

string catalogConnectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' was not found.");

builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    options.UseNpgsql(catalogConnectionString);
});

builder.Services
    .AddHealthChecks()
    .AddEshopPostgreSqlReadinessCheck(
        catalogConnectionString);

WebApplication app = builder.Build();

app.UseEshopErrorHandling();
app.UseEshopOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapEshopHealthChecks();

app.Run();
