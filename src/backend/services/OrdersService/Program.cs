using Asp.Versioning;
using ErrorHandling.Shared;
using Microsoft.EntityFrameworkCore;
using OpenApi.Shared;
using OrdersService.Application;
using OrdersService.Data;
using OrdersService.Identity;
using OrdersService.Integration;
using OrdersService.Options;

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

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopOpenApi(
    title: "Eshop Orders API",
    description:
        "Customer checkout and order query API.");

builder.Services
    .AddOptions<OrdersOptions>()
    .BindConfiguration(OrdersOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(
            options.DevelopmentCustomerHeaderName),
        "Development customer header name must be configured.")
    .ValidateOnStart();

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

builder.Services.AddHttpClient<IBasketClient, BasketClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(basketBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<OrderApplicationService>();
builder.Services.AddSingleton<IOrderOwnerProvider, OrderOwnerProvider>();

WebApplication app = builder.Build();

app.UseEshopErrorHandling();
app.UseEshopOpenApi();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
