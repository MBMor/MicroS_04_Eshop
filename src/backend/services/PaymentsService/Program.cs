using Asp.Versioning;
using ErrorHandling.Shared;
using Eshop.Observability;
using Messaging.Shared;
using Messaging.Shared.Outbox;
using Microsoft.EntityFrameworkCore;
using OpenApi.Shared;
using PaymentsService.Application;
using PaymentsService.Data;
using PaymentsService.Messaging;
using PaymentsService.Outbox;
using Eshop.Security.Authentication;
using Eshop.Security.Authorization;

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
    serviceName: "payments-service");

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopOpenApi(
    title: "Eshop Payments API",
    description:
        "Fake payment processing and payment query API.");

builder.Services.AddEshopJwtAuthentication(
    builder.Configuration);

builder.Services.AddEshopAuthorization();

string paymentsConnectionString =
    builder.Configuration.GetConnectionString("PaymentsDb")
    ?? throw new InvalidOperationException(
        "Connection string 'PaymentsDb' was not found.");

builder.Services.AddDbContext<PaymentsDbContext>(
    options =>
    {
        options.UseNpgsql(paymentsConnectionString);
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
builder.Services.AddSingleton<FakePaymentProcessor>();
builder.Services.AddScoped<PaymentApplicationService>();

builder.Services.AddEshopMessagingCore(builder.Configuration);

builder.Services.AddSingleton<PaymentsOutboxWriter>();

builder.Services.AddScoped<PaymentsOutboxStore>();

builder.Services.AddScoped<PaymentRequestedProcessingService>();

builder.Services.AddHostedService<PaymentRequestedConsumerWorker>();
builder.Services.AddHostedService<PaymentsOutboxPublisherWorker>();
builder.Services.AddHostedService<PaymentsOutboxCleanupWorker>();

WebApplication app = builder.Build();

app.UseEshopErrorHandling();

app.UseEshopOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers()
    .RequireAuthorization(
        EshopPolicies.SupportOrAdmin);

app.MapHealthChecks("/health");

app.Run();
