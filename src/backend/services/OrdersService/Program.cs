using Asp.Versioning;
using ErrorHandling.Shared;
using Eshop.Observability;
using Messaging.Shared;
using Messaging.Shared.Outbox;
using Microsoft.EntityFrameworkCore;
using OpenApi.Shared;
using OrdersService.Application;
using OrdersService.Data;
using OrdersService.Identity;
using OrdersService.Integration;
using OrdersService.Messaging;
using OrdersService.Outbox;
using Eshop.Security.Authentication;
using Eshop.Security.Authorization;

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

builder.Services.AddHealthChecks();

builder.Services.AddEshopObservability(
    builder.Configuration,
    serviceName: "orders-service");

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopOpenApi(
    title: "Eshop Orders API",
    description:
        "Customer checkout and order query API.");

builder.Services.AddEshopJwtAuthentication(
    builder.Configuration);

builder.Services.AddEshopAuthorization();

string ordersConnectionString =
    builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException(
        "Connection string 'OrdersDb' was not found.");

builder.Services.AddDbContext<OrdersDbContext>(options =>
{
    options.UseNpgsql(ordersConnectionString);
});

string basketBaseUrl =
    builder.Configuration["Services:BasketBaseUrl"]
    ?? throw new InvalidOperationException(
        "Configuration value 'Services:BasketBaseUrl' was not found.");

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<IBasketClient, BasketClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(basketBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(5);
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

builder.Services.AddScoped<OrderApplicationService>();
builder.Services.AddSingleton<IOrderOwnerProvider, OrderOwnerProvider>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddEshopMessagingCore(builder.Configuration);

builder.Services.AddSingleton<OrdersOutboxWriter>();

builder.Services.AddScoped<OrdersOutboxStore>();

builder.Services.AddScoped<OrderStockResultService>();

builder.Services.AddScoped<IOrderStockResultService>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            OrderStockResultService>());

builder.Services.AddHostedService<OrdersOutboxPublisherWorker>();
builder.Services.AddHostedService<OrdersOutboxCleanupWorker>();
builder.Services.AddHostedService<StockReservedConsumerWorker>();
builder.Services.AddHostedService<StockReservationFailedConsumerWorker>();

builder.Services.AddScoped<OrderPaymentResultService>();

builder.Services.AddHostedService<PaymentAuthorizedConsumerWorker>();
builder.Services.AddHostedService<PaymentFailedConsumerWorker>();
builder.Services.AddHostedService<StockReleasedConsumerWorker>();

WebApplication app = builder.Build();

app.UseEshopErrorHandling();
app.UseEshopOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers()
    .RequireAuthorization(
        EshopPolicies.CustomerOnly);

app.MapHealthChecks("/health");

app.Run();
