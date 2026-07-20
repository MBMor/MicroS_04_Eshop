using Asp.Versioning;
using ErrorHandling.Shared;
using Eshop.Observability;
using InventoryService.Application;
using InventoryService.Data;
using InventoryService.Messaging;
using InventoryService.Outbox;
using Messaging.Shared;
using Messaging.Shared.Outbox;
using Microsoft.EntityFrameworkCore;
using OpenApi.Shared;

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

builder.Services.AddEshopObservability(
    builder.Configuration,
    serviceName: "inventory-service");

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

builder.Services
    .AddOptions<OutboxProcessingOptions>()
    .Bind(
        builder.Configuration.GetSection(
            OutboxProcessingOptions.SectionName))
    .Validate(
        options => options.BatchSize is >= 1 and <= 500,
        "Outbox batch size must be between 1 and 500.")
    .Validate(
        options => options.PollingInterval
            >= TimeSpan.FromMilliseconds(100),
        "Outbox polling interval must be at least 100 milliseconds.")
    .Validate(
        options => options.ClaimTimeout
            >= TimeSpan.FromSeconds(30),
        "Outbox claim timeout must be at least 30 seconds.")
    .Validate(
        options => options.MaximumRetryCount
            is >= 1 and <= 100,
        "Outbox maximum retry count must be between 1 and 100.")
    .Validate(
        options => options.InitialRetryDelay
            >= TimeSpan.FromMilliseconds(100),
        "Outbox initial retry delay must be at least 100 milliseconds.")
    .Validate(
        options => options.MaximumRetryDelay
            >= options.InitialRetryDelay,
        "Outbox maximum retry delay must be greater than or equal to the initial retry delay.")
    .Validate(
        options => options.PublishedRetention
            >= TimeSpan.FromHours(1),
        "Outbox retention must be at least one hour.")
    .Validate(
        options => options.CleanupInterval
            >= TimeSpan.FromMinutes(1),
        "Outbox cleanup interval must be at least one minute.")
    .Validate(
        options => options.CleanupBatchSize
            is >= 1 and <= 10_000,
        "Outbox cleanup batch size must be between 1 and 10000.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<InventoryApplicationService>();
builder.Services.AddEshopMessagingCore(builder.Configuration);

builder.Services.AddSingleton<InventoryOutboxWriter>();

builder.Services.AddScoped<InventoryOutboxStore>();

builder.Services.AddScoped<OrderStockReservationService>();

builder.Services.AddHostedService<OrderCreatedConsumerWorker>();
builder.Services.AddHostedService<InventoryOutboxPublisherWorker>();
builder.Services.AddHostedService<InventoryOutboxCleanupWorker>();

builder.Services.AddScoped<OrderStockReleaseService>();
builder.Services.AddHostedService<StockReleaseRequestedConsumerWorker>();

WebApplication app = builder.Build();

app.UseEshopErrorHandling();
app.UseEshopOpenApi();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
