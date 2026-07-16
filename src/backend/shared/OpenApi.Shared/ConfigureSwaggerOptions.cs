using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenApi.Shared;

internal sealed class ConfigureSwaggerOptions(
    IApiVersionDescriptionProvider apiVersionDescriptionProvider,
    IOptions<EshopOpenApiOptions> openApiOptions)
    : IConfigureOptions<SwaggerGenOptions>
{
    private readonly EshopOpenApiOptions _openApiOptions =
        openApiOptions.Value;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (ApiVersionDescription description
                 in apiVersionDescriptionProvider
                     .ApiVersionDescriptions)
        {
            options.SwaggerDoc(
                description.GroupName,
                CreateOpenApiInfo(description));
        }
    }

    private OpenApiInfo CreateOpenApiInfo(
        ApiVersionDescription description)
    {
        OpenApiInfo info = new()
        {
            Title = _openApiOptions.Title,
            Version = description.ApiVersion.ToString(),
            Description = _openApiOptions.Description
        };

        if (description.IsDeprecated)
        {
            info.Description +=
                " This API version is deprecated.";
        }

        return info;
    }
}
