using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.Api.Inventory;

public sealed partial class InventoryApiClient
    : IInventoryApiClient
{
    private const string HttpClientName =
        "ApiGatewayAuthenticated";

    private const string InventoryItemsPath =
        "api/v1/inventory-items";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InventoryApiClient> _logger;

    public InventoryApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<InventoryApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(
            httpClientFactory);

        ArgumentNullException.ThrowIfNull(
            logger);

        _httpClientFactory =
            httpClientFactory;

        _logger =
            logger;
    }

    public async Task<IReadOnlyList<InventoryItemDto>>
        GetInventoryItemsAsync(
            bool includeInactive,
            CancellationToken cancellationToken)
    {
        string requestPath =
            includeInactive
                ? $"{InventoryItemsPath}?includeInactive=true"
                : InventoryItemsPath;

        using HttpClient httpClient =
            _httpClientFactory.CreateClient(
                HttpClientName);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                requestPath);

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
                    _logger,
                    requestPath,
                    (int)response.StatusCode,
                    problemDetails?.ErrorCode,
                    problemDetails?.TraceId,
                    problemDetails?.RequestId);

                throw new ApiRequestException(
                    response.StatusCode,
                    problemDetails);
            }

            InventoryItemDto[]? items =
                await response.Content
                    .ReadFromJsonAsync<InventoryItemDto[]>(
                        JsonOptions,
                        cancellationToken);

            return items
                ?? throw new JsonException(
                    "Inventory response body was empty.");
        }
        catch (HttpRequestException exception)
        {
            LogTransportFailure(
                _logger,
                exception,
                requestPath);

            throw;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(
                _logger,
                exception,
                requestPath);

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
        EventId = 5000,
        Level = LogLevel.Warning,
        Message =
            "Inventory request {RequestPath} failed with HTTP {StatusCode}. " +
            "ErrorCode={ErrorCode} TraceId={TraceId} RequestId={RequestId}")]
    private static partial void LogHttpFailure(
        ILogger logger,
        string requestPath,
        int statusCode,
        string? errorCode,
        string? traceId,
        string? requestId);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message =
            "Inventory request {RequestPath} failed before receiving an HTTP response.")]
    private static partial void LogTransportFailure(
        ILogger logger,
        Exception exception,
        string requestPath);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Warning,
        Message =
            "Inventory request {RequestPath} exceeded the configured timeout.")]
    private static partial void LogTimeout(
        ILogger logger,
        Exception exception,
        string requestPath);
}
