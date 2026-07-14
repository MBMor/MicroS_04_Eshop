using CatalogService.Data;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

string catalogConnectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' was not found.");

builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    options.UseNpgsql(catalogConnectionString);
});

WebApplication app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
