using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ErrorHandling.Shared;

public static class ErrorHandlingServiceCollectionExtensions
{
    public static IServiceCollection AddEshopErrorHandling(
        this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                HttpContext httpContext =
                    context.HttpContext;

                context.ProblemDetails.Instance ??=
                    httpContext.Request.Path;

                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.Id
                    ?? httpContext.TraceIdentifier;

                context.ProblemDetails.Extensions["requestId"] =
                    httpContext.TraceIdentifier;
            };
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory =
                actionContext =>
                {
                    ValidationProblemDetails problemDetails =
                        new(actionContext.ModelState)
                        {
                            Status =
                                StatusCodes.Status400BadRequest,
                            Type =
                                "https://httpstatuses.com/400",
                            Title =
                                "Request validation failed.",
                            Detail =
                                "One or more validation errors occurred.",
                            Instance =
                                actionContext.HttpContext.Request.Path
                        };

                    problemDetails.Extensions["errorCode"] =
                        "model_validation_failed";

                    problemDetails.Extensions["traceId"] =
                        Activity.Current?.Id
                        ?? actionContext.HttpContext.TraceIdentifier;

                    problemDetails.Extensions["requestId"] =
                        actionContext.HttpContext.TraceIdentifier;

                    return new BadRequestObjectResult(
                        problemDetails)
                    {
                        ContentTypes =
                        {
                            "application/problem+json"
                        }
                    };
                };
        });

        return services;
    }
}
