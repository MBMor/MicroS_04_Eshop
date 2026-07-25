using Microsoft.AspNetCore.Builder;

namespace Eshop.ErrorHandling;

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
