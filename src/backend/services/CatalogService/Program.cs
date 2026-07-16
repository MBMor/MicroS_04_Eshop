using Asp.Versioning;
using CatalogService.Data;
using ErrorHandling.Shared;
using Microsoft.EntityFrameworkCore;

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
    .AddMvc();

builder.Services.AddHealthChecks();

builder.Services.AddEshopErrorHandling();

string catalogConnectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' was not found.");

builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    options.UseNpgsql(catalogConnectionString);
});

WebApplication app = builder.Build();

app.UseEshopErrorHandling();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
