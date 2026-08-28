using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.Api.Catalog;

public sealed partial class CatalogApiClient : ICatalogApiClient
{
    private const string HttpClientName = "ApiGateway";
    private const string ProductsPath = "api/v1/products";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CatalogApiClient> _logger;

    public CatalogApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<CatalogApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CatalogProductDto>> GetProductsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        string requestPath = includeInactive
            ? $"{ProductsPath}?includeInactive=true"
            : ProductsPath;

        using HttpClient httpClient =
            _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(
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

            CatalogProductDto[]? products =
                await response.Content
                    .ReadFromJsonAsync<CatalogProductDto[]>(
                        JsonOptions,
                        cancellationToken);

            return products
                ?? throw new JsonException(
                    "Catalog response body was empty.");
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
        EventId = 2000,
        Level = LogLevel.Warning,
        Message =
            "Catalog request {RequestPath} failed with HTTP {StatusCode}. " +
            "ErrorCode={ErrorCode} TraceId={TraceId} RequestId={RequestId}")]
    private static partial void LogHttpFailure(
        ILogger logger,
        string requestPath,
        int statusCode,
        string? errorCode,
        string? traceId,
        string? requestId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message =
            "Catalog request {RequestPath} failed before receiving " +
            "an HTTP response.")]
    private static partial void LogTransportFailure(
        ILogger logger,
        Exception exception,
        string requestPath);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message =
            "Catalog request {RequestPath} exceeded the configured timeout.")]
    private static partial void LogTimeout(
        ILogger logger,
        Exception exception,
        string requestPath);
}
