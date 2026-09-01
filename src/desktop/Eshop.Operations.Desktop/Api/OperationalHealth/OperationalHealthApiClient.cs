using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.Api.OperationalHealth;

public sealed partial class OperationalHealthApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<OperationalHealthApiClient> logger)
    : IOperationalHealthApiClient
{
    private const string HttpClientName =
        "ApiGatewayAuthenticated";

    private const string RequestPath =
        "api/v1/operations/health";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<OperationalHealthResponseDto>
        GetOperationalHealthAsync(
            CancellationToken cancellationToken)
    {
        using HttpClient httpClient =
            httpClientFactory.CreateClient(
                HttpClientName);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                RequestPath);

        try
        {
            using HttpResponseMessage response =
                await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                ApiProblemDetails? problemDetails =
                    await TryReadProblemDetailsAsync(
                        response,
                        cancellationToken);

                LogHttpFailure(
                    logger,
                    (int)response.StatusCode,
                    problemDetails?.TraceId,
                    problemDetails?.RequestId);

                throw new ApiRequestException(
                    response.StatusCode,
                    problemDetails);
            }

            OperationalHealthResponseDto? result =
                await response.Content
                    .ReadFromJsonAsync<
                        OperationalHealthResponseDto>(
                            JsonOptions,
                            cancellationToken);

            return result
                ?? throw new JsonException(
                    "Operational health response body was empty.");
        }
        catch (HttpRequestException exception)
        {
            LogTransportFailure(
                logger,
                exception);

            throw;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(
                logger,
                exception);

            throw;
        }
    }

    private static async Task<ApiProblemDetails?>
        TryReadProblemDetailsAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content
                .ReadFromJsonAsync<ApiProblemDetails>(
                    JsonOptions,
                    cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    [LoggerMessage(
        EventId = 5700,
        Level = LogLevel.Warning,
        Message =
            "Operational health request failed with HTTP {StatusCode}. " +
            "TraceId={TraceId} RequestId={RequestId}")]
    private static partial void LogHttpFailure(
        ILogger logger,
        int statusCode,
        string? traceId,
        string? requestId);

    [LoggerMessage(
        EventId = 5701,
        Level = LogLevel.Warning,
        Message =
            "Operational health request failed before receiving an HTTP response.")]
    private static partial void LogTransportFailure(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 5702,
        Level = LogLevel.Warning,
        Message =
            "Operational health request exceeded the configured HTTP timeout.")]
    private static partial void LogTimeout(
        ILogger logger,
        Exception exception);
}
