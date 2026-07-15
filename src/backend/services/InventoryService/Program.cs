using InventoryService.Application;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

string inventoryConnectionString =
    builder.Configuration.GetConnectionString("InventoryDb")
    ?? throw new InvalidOperationException(
        "Connection string 'InventoryDb' was not found.");

builder.Services.AddDbContext<InventoryDbContext>(
    options =>
    {
        options.UseNpgsql(inventoryConnectionString);
    });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<InventoryApplicationService>();

WebApplication app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
