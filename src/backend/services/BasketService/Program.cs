using Asp.Versioning;
using BasketService.Application;
using BasketService.Data;
using BasketService.Identity;
using BasketService.Integration;
using BasketService.Options;
using Eshop.ErrorHandling;
using Eshop.HealthChecks;
using Eshop.OpenApi;
using Eshop.Observability;
using Eshop.Security.Authentication;
using Eshop.Security.Authorization;
using Microsoft.Extensions.Caching.Distributed;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEshopObservability(
    builder.Configuration,
    serviceName: "basket-service");

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

builder.Services.AddEshopErrorHandling();

builder.Services.AddEshopOpenApi(
    title: "Eshop Basket API",
    description:
        "Customer shopping basket management API.");

builder.Services.AddEshopJwtAuthentication(
    builder.Configuration);

builder.Services.AddEshopAuthorization();

builder.Services
    .AddOptions<BasketOptions>()
    .BindConfiguration(BasketOptions.SectionName)
    .Validate(
        options => options.ExpirationMinutes > 0,
        "Basket expiration must be greater than zero.")
    .Validate(
        options => options.MaxQuantityPerItem > 0,
        "Maximum quantity per item must be greater than zero.")
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

builder.Services
    .AddHealthChecks()
    .AddEshopReadinessCheck(
        "redis",
        async (serviceProvider, cancellationToken) =>
        {
            IDistributedCache cache = serviceProvider
                .GetRequiredService<IDistributedCache>();

            await cache.GetAsync(
                "__eshop_readiness__",
                cancellationToken);
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

app.UseEshopErrorHandling();
app.UseEshopOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers()
    .RequireAuthorization(
        EshopPolicies.CustomerOnly);

app.MapEshopHealthChecks();

app.Run();
