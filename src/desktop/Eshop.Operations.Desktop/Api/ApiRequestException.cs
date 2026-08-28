using System.Net;

namespace Eshop.Operations.Desktop.Api;

public sealed class ApiProblemDetails
{
    public int? Status { get; init; }

    public string? Title { get; init; }

    public string? Detail { get; init; }

    public string? ErrorCode { get; init; }

    public string? TraceId { get; init; }

    public string? RequestId { get; init; }
}

public sealed class ApiRequestException : Exception
{
    public ApiRequestException()
    {
    }

    public ApiRequestException(string message)
        : base(message)
    {
    }

    public ApiRequestException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }

    public ApiRequestException(
        HttpStatusCode statusCode,
        ApiProblemDetails? problemDetails)
        : base(
            $"API request failed with HTTP status " +
            $"{(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        ProblemDetails = problemDetails;
    }

    public HttpStatusCode? StatusCode { get; }

    public ApiProblemDetails? ProblemDetails { get; }
}
