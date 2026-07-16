using Asp.Versioning;
using ErrorHandling.Shared;
using Microsoft.EntityFrameworkCore;
using NotificationsService.Application;
using NotificationsService.Data;
using NotificationsService.Identity;
using NotificationsService.Options;
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

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopOpenApi(
    title: "Eshop Notifications API",
    description:
        "Customer notification read model API.");

builder.Services
    .AddOptions<NotificationsOptions>()
    .BindConfiguration(NotificationsOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(
            options.DevelopmentCustomerHeaderName),
        "Development customer header name must be configured.")
    .ValidateOnStart();

string notificationsConnectionString =
    builder.Configuration.GetConnectionString("NotificationsDb")
    ?? throw new InvalidOperationException(
        "Connection string 'NotificationsDb' was not found.");

builder.Services.AddDbContext<NotificationsDbContext>(
    options =>
    {
        options.UseNpgsql(notificationsConnectionString);
    });

builder.Services.AddScoped<NotificationApplicationService>();

builder.Services.AddSingleton<
    INotificationOwnerProvider,
    NotificationOwnerProvider>();

WebApplication app = builder.Build();

app.UseEshopErrorHandling();
app.UseEshopOpenApi();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
