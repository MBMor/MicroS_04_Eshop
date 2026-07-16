using ErrorHandling.Shared.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ErrorHandling.Shared;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ExceptionMapping mapping = MapException(exception);

        LogException(
            exception,
            mapping,
            httpContext);

        ProblemDetails problemDetails = new()
        {
            Status = mapping.StatusCode,
            Type = $"https://httpstatuses.com/{mapping.StatusCode}",
            Title = mapping.Title,
            Detail = mapping.Detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["errorCode"] =
            mapping.ErrorCode;

        httpContext.Response.StatusCode =
            mapping.StatusCode;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception
            });
    }

    private void LogException(
        Exception exception,
        ExceptionMapping mapping,
        HttpContext httpContext)
    {
        if (mapping.StatusCode >=
            StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}. " +
                "TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);

            return;
        }

        logger.LogWarning(
            exception,
            "Request failed with status {StatusCode} while processing " +
            "{Method} {Path}. ErrorCode: {ErrorCode}. TraceId: {TraceId}",
            mapping.StatusCode,
            httpContext.Request.Method,
            httpContext.Request.Path,
            mapping.ErrorCode,
            httpContext.TraceIdentifier);
    }

    private static ExceptionMapping MapException(
        Exception exception)
    {
        return exception switch
        {
            RequestValidationException validationException
                => new ExceptionMapping(
                    StatusCodes.Status400BadRequest,
                    "Request validation failed.",
                    validationException.Message,
                    validationException.ErrorCode),

            ResourceNotFoundException notFoundException
                => new ExceptionMapping(
                    StatusCodes.Status404NotFound,
                    "Resource was not found.",
                    notFoundException.Message,
                    notFoundException.ErrorCode),

            ResourceConflictException conflictException
                => new ExceptionMapping(
                    StatusCodes.Status409Conflict,
                    "Resource conflict.",
                    conflictException.Message,
                    conflictException.ErrorCode),

            BadHttpRequestException badRequestException
                => new ExceptionMapping(
                    StatusCodes.Status400BadRequest,
                    "Invalid HTTP request.",
                    badRequestException.Message,
                    "invalid_http_request"),

            TimeoutException
                => new ExceptionMapping(
                    StatusCodes.Status503ServiceUnavailable,
                    "Service temporarily unavailable.",
                    "The operation timed out. Try the request again later.",
                    "operation_timeout"),

            HttpRequestException
                => new ExceptionMapping(
                    StatusCodes.Status502BadGateway,
                    "Upstream service request failed.",
                    "A dependent service could not complete the request.",
                    "upstream_service_error"),

            _ => new ExceptionMapping(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "The server could not process the request.",
                "unexpected_error")
        };
    }

    private sealed record ExceptionMapping(
        int StatusCode,
        string Title,
        string Detail,
        string ErrorCode);
}
