using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenApi.Shared;

public static class OpenApiServiceCollectionExtensions
{
    public static IServiceCollection AddEshopOpenApi(
        this IServiceCollection services,
        string title,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        services.Configure<EshopOpenApiOptions>(options =>
        {
            options.Title = title;
            options.Description = description;
        });

        services.AddSwaggerGen(options =>
        {
            options.SupportNonNullableReferenceTypes();

            options.CustomSchemaIds(type =>
                type.FullName?.Replace('+', '.')
                ?? type.Name);

            options.OrderActionsBy(apiDescription =>
                apiDescription.RelativePath);
        });

        services.AddTransient<
            IConfigureOptions<SwaggerGenOptions>,
            ConfigureSwaggerOptions>();

        return services;
    }
}
