using Microsoft.EntityFrameworkCore;
using NotificationsService.Application;
using NotificationsService.Data;
using NotificationsService.Identity;
using NotificationsService.Options;

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

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

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
