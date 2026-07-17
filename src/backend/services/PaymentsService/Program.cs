using Asp.Versioning;
using ErrorHandling.Shared;
using Microsoft.EntityFrameworkCore;
using OpenApi.Shared;
using PaymentsService.Application;
using PaymentsService.Data;
using Messaging.Shared;
using PaymentsService.Messaging;
using PaymentsService.Outbox;

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
    title: "Eshop Payments API",
    description:
        "Fake payment processing and payment query API.");

string paymentsConnectionString =
    builder.Configuration.GetConnectionString("PaymentsDb")
    ?? throw new InvalidOperationException(
        "Connection string 'PaymentsDb' was not found.");

builder.Services.AddDbContext<PaymentsDbContext>(
    options =>
    {
        options.UseNpgsql(paymentsConnectionString);
    });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FakePaymentProcessor>();
builder.Services.AddScoped<PaymentApplicationService>();

builder.Services.AddEshopMessagingCore(builder.Configuration);

builder.Services.AddSingleton<PaymentsOutboxWriter>();
builder.Services.AddScoped<PaymentRequestedProcessingService>();

builder.Services.AddHostedService<PaymentRequestedConsumerWorker>();
builder.Services.AddHostedService<PaymentsOutboxPublisherWorker>();

WebApplication app = builder.Build();

app.UseEshopErrorHandling();
app.UseEshopOpenApi();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
