using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.Api.Orders;

public sealed partial class OrdersApiClient
    : IOrdersApiClient
{
    private const string HttpClientName =
        "ApiGatewayAuthenticated";

    private const string OperationalOrdersPath =
        "api/v1/operations/orders";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OrdersApiClient> _logger;

    public OrdersApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<OrdersApiClient> logger)
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

    public Task<OperationalOrderPageDto> GetOrdersAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "Offset must not be negative.");
        }

        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Limit must be between 1 and 100.");
        }

        string requestPath =
            FormattableString.Invariant(
                $"{OperationalOrdersPath}?offset={offset}&limit={limit}");

        return GetAsync<OperationalOrderPageDto>(
            requestPath,
            cancellationToken);
    }

    public Task<OperationalOrderDetailDto> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must not be empty.",
                nameof(orderId));
        }

        string requestPath =
            $"{OperationalOrdersPath}/{orderId:D}";

        return GetAsync<OperationalOrderDetailDto>(
            requestPath,
            cancellationToken);
    }

    private async Task<T> GetAsync<T>(
        string requestPath,
        CancellationToken cancellationToken)
    {
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

            T? result =
                await response.Content
                    .ReadFromJsonAsync<T>(
                        JsonOptions,
                        cancellationToken);

            return result
                ?? throw new JsonException(
                    $"Orders response body for '{requestPath}' was empty.");
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
        EventId = 5400,
        Level = LogLevel.Warning,
        Message =
            "Orders request {RequestPath} failed with HTTP {StatusCode}. " +
            "ErrorCode={ErrorCode} TraceId={TraceId} RequestId={RequestId}")]
    private static partial void LogHttpFailure(
        ILogger logger,
        string requestPath,
        int statusCode,
        string? errorCode,
        string? traceId,
        string? requestId);

    [LoggerMessage(
        EventId = 5401,
        Level = LogLevel.Warning,
        Message =
            "Orders request {RequestPath} failed before receiving an HTTP response.")]
    private static partial void LogTransportFailure(
        ILogger logger,
        Exception exception,
        string requestPath);

    [LoggerMessage(
        EventId = 5402,
        Level = LogLevel.Warning,
        Message =
            "Orders request {RequestPath} exceeded the configured timeout.")]
    private static partial void LogTimeout(
        ILogger logger,
        Exception exception,
        string requestPath);
}
