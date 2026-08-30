using System.Net;
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

    private const string IdempotencyKeyHeaderName =
        "Idempotency-Key";

    private const string IdempotentReplayHeaderName =
        "Idempotent-Replay";

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

    public async Task<InventoryStockAdjustmentHistoryPageDto>
        GetStockAdjustmentHistoryAsync(
            Guid inventoryItemId,
            int offset,
            int limit,
            CancellationToken cancellationToken)
    {
        if (inventoryItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "Inventory item id must not be empty.",
                nameof(inventoryItemId));
        }

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
                $"{InventoryItemsPath}/{inventoryItemId:D}/stock-adjustments?offset={offset}&limit={limit}");

        using HttpClient httpClient =
            _httpClientFactory.CreateClient(HttpClientName);

        using var request =
            new HttpRequestMessage(HttpMethod.Get, requestPath);

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
                    await TryReadProblemDetailsAsync(response, cancellationToken);

                LogHttpFailure(
                    _logger,
                    requestPath,
                    (int)response.StatusCode,
                    problemDetails?.ErrorCode,
                    problemDetails?.TraceId,
                    problemDetails?.RequestId);

                throw new ApiRequestException(response.StatusCode, problemDetails);
            }

            InventoryStockAdjustmentHistoryPageDto? page =
                await response.Content.ReadFromJsonAsync<InventoryStockAdjustmentHistoryPageDto>(
                    JsonOptions,
                    cancellationToken);

            return page
                ?? throw new JsonException(
                    "Inventory stock adjustment history response body was empty.");
        }
        catch (HttpRequestException exception)
        {
            LogTransportFailure(_logger, exception, requestPath);
            throw;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(_logger, exception, requestPath);
            throw;
        }
    }

    public async Task<InventoryStockAdjustmentResult>
        AdjustStockAsync(
            InventoryStockAdjustmentRequest request,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InventoryItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "Inventory item id must not be empty.",
                nameof(request));
        }

        if (request.QuantityDelta == 0)
        {
            throw new ArgumentException(
                "Quantity delta must not be zero.",
                nameof(request));
        }

        if (request.ExpectedVersion == 0)
        {
            throw new ArgumentException(
                "Expected version must be greater than zero.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException(
                "Reason must not be empty.",
                nameof(request));
        }

        if (request.IdempotencyKey == Guid.Empty)
        {
            throw new ArgumentException(
                "Idempotency key must not be empty.",
                nameof(request));
        }

        string requestPath =
            $"{InventoryItemsPath}/{request.InventoryItemId}/stock-adjustments";

        using HttpClient httpClient =
            _httpClientFactory.CreateClient(HttpClientName);

        using var httpRequest =
            new HttpRequestMessage(HttpMethod.Post, requestPath);

        httpRequest.Headers.Add(
            IdempotencyKeyHeaderName,
            request.IdempotencyKey.ToString("D"));

        httpRequest.Content = JsonContent.Create(
            new
            {
                quantityDelta = request.QuantityDelta,
                expectedVersion = request.ExpectedVersion,
                reason = request.Reason
            },
            options: JsonOptions);

        bool sendAttemptStarted = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            sendAttemptStarted = true;

            using HttpResponseMessage response =
                await httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (IsUnknownOutcomeStatusCode(response.StatusCode))
            {
                LogUnknownStockAdjustmentOutcome(
                    _logger,
                    requestPath,
                    (int)response.StatusCode);

                throw new InventoryStockAdjustmentOutcomeUnknownException(
                    request.IdempotencyKey,
                    "The stock adjustment result is unknown. " +
                    "Retry only with the same operation identifier.",
                    new HttpRequestException(
                        $"Stock adjustment returned HTTP {(int)response.StatusCode}."));
            }

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

            InventoryItemDto? item =
                await response.Content.ReadFromJsonAsync<InventoryItemDto>(
                    JsonOptions,
                    cancellationToken);

            if (item is null)
            {
                throw new JsonException(
                    "Stock adjustment response body was empty.");
            }

            bool isReplay =
                response.Headers.TryGetValues(
                    IdempotentReplayHeaderName,
                    out IEnumerable<string>? replayValues)
                && replayValues.Any(
                    value => string.Equals(
                        value,
                        "true",
                        StringComparison.OrdinalIgnoreCase));

            return new InventoryStockAdjustmentResult(
                item,
                isReplay,
                request.IdempotencyKey);
        }
        catch (InventoryStockAdjustmentOutcomeUnknownException)
        {
            throw;
        }
        catch (HttpRequestException exception)
            when (sendAttemptStarted)
        {
            LogUnknownStockAdjustmentTransportFailure(
                _logger,
                exception,
                requestPath);

            throw new InventoryStockAdjustmentOutcomeUnknownException(
                request.IdempotencyKey,
                "The stock adjustment result is unknown because the HTTP request did not complete reliably.",
                exception);
        }
        catch (OperationCanceledException exception)
            when (sendAttemptStarted)
        {
            LogUnknownStockAdjustmentTransportFailure(
                _logger,
                exception,
                requestPath);

            throw new InventoryStockAdjustmentOutcomeUnknownException(
                request.IdempotencyKey,
                "The stock adjustment result is unknown because waiting for the HTTP response was canceled.",
                exception);
        }
    }

    private static bool IsUnknownOutcomeStatusCode(
        HttpStatusCode statusCode)
    {
        int numericStatusCode = (int)statusCode;

        return statusCode == HttpStatusCode.RequestTimeout
            || numericStatusCode >= 500;
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

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Warning,
        Message =
            "Inventory stock adjustment {RequestPath} returned HTTP {StatusCode} with an unknown write outcome.")]
    private static partial void LogUnknownStockAdjustmentOutcome(
        ILogger logger,
        string requestPath,
        int statusCode);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Warning,
        Message =
            "Inventory stock adjustment {RequestPath} ended before a reliable response was received.")]
    private static partial void LogUnknownStockAdjustmentTransportFailure(
        ILogger logger,
        Exception exception,
        string requestPath);
}
