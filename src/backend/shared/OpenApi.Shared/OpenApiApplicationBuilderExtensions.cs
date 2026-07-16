using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OpenApi.Shared;

public static class OpenApiApplicationBuilderExtensions
{
    public static WebApplication UseEshopOpenApi(
        this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.UseSwagger();

        IApiVersionDescriptionProvider versionProvider =
            app.Services.GetRequiredService<
                IApiVersionDescriptionProvider>();

        EshopOpenApiOptions openApiOptions =
            app.Services
                .GetRequiredService<
                    IOptions<EshopOpenApiOptions>>()
                .Value;

        app.UseSwaggerUI(options =>
        {
            foreach (ApiVersionDescription description
                     in versionProvider.ApiVersionDescriptions)
            {
                string documentName =
                    description.GroupName;

                string displayName =
                    $"{openApiOptions.Title} " +
                    $"{documentName.ToUpperInvariant()}";

                options.SwaggerEndpoint(
                    $"/swagger/{documentName}/swagger.json",
                    displayName);
            }

            options.RoutePrefix = "swagger";

            options.DocumentTitle =
                $"{openApiOptions.Title} API documentation";

            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.ShowExtensions();
            options.ShowCommonExtensions();
        });

        return app;
    }
}
