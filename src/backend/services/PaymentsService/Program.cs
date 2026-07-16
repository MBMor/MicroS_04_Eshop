using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Application;
using PaymentsService.Data;

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
    .AddMvc();

builder.Services.AddHealthChecks();

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

WebApplication app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
