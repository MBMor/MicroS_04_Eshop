using Microsoft.AspNetCore.Builder;

namespace ErrorHandling.Shared;

public static class ErrorHandlingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEshopErrorHandling(
        this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }
}
