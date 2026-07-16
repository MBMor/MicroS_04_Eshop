using Asp.Versioning;
using BasketService.Application;
using BasketService.Data;
using BasketService.Identity;
using BasketService.Integration;
using BasketService.Options;

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
    .AddMvc();

builder.Services.AddHealthChecks();

builder.Services
    .AddOptions<BasketOptions>()
    .BindConfiguration(BasketOptions.SectionName)
    .Validate(
        options => options.ExpirationMinutes > 0,
        "Basket expiration must be greater than zero.")
    .Validate(
        options => options.MaxQuantityPerItem > 0,
        "Maximum quantity per item must be greater than zero.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(
            options.DevelopmentCustomerHeaderName),
        "Development customer header name must be configured.")
    .ValidateOnStart();

string redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Connection string 'Redis' was not found.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "eshop:";
});

string catalogBaseUrl =
    builder.Configuration["Services:CatalogBaseUrl"]
    ?? throw new InvalidOperationException(
        "Configuration value 'Services:CatalogBaseUrl' was not found.");

builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(catalogBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IBasketRepository, RedisBasketRepository>();
builder.Services.AddScoped<BasketApplicationService>();
builder.Services.AddSingleton<IBasketOwnerProvider, BasketOwnerProvider>();

WebApplication app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
