using Asp.Versioning;
using ErrorHandling.Shared;
using InventoryService.Application;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using OpenApi.Shared;
using InventoryService.Messaging;
using InventoryService.Outbox;
using Messaging.Shared;

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

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

builder.Services.AddHealthChecks();

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopOpenApi(
    title: "Eshop Inventory API",
    description:
        "Inventory item and stock management API.");

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
builder.Services.AddEshopMessagingCore(builder.Configuration);

builder.Services.AddSingleton<InventoryOutboxWriter>();
builder.Services.AddScoped<OrderStockReservationService>();

builder.Services.AddHostedService<OrderCreatedConsumerWorker>();
builder.Services.AddHostedService<InventoryOutboxPublisherWorker>();

WebApplication app = builder.Build();

app.UseEshopErrorHandling();
app.UseEshopOpenApi();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
