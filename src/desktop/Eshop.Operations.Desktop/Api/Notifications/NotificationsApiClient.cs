using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.Api.Notifications;

public sealed partial class NotificationsApiClient
    : INotificationsApiClient
{
    private const string HttpClientName =
        "ApiGatewayAuthenticated";

    private const string OperationalNotificationsPath =
        "api/v1/operations/notifications";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationsApiClient> _logger;

    public NotificationsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationsApiClient> logger)
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

    public Task<OperationalNotificationPageDto>
        GetNotificationsAsync(
            Guid? orderId,
            string? customerId,
            Guid? correlationId,
            int offset,
            int limit,
            CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must not be empty.",
                nameof(orderId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation id must not be empty.",
                nameof(correlationId));
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
            BuildListRequestPath(
                orderId,
                customerId,
                correlationId,
                offset,
                limit);

        return GetAsync<OperationalNotificationPageDto>(
            requestPath,
            cancellationToken);
    }

    public Task<OperationalNotificationDto>
        GetNotificationAsync(
            Guid notificationId,
            CancellationToken cancellationToken)
    {
        if (notificationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Notification id must not be empty.",
                nameof(notificationId));
        }

        string requestPath =
            $"{OperationalNotificationsPath}/{notificationId:D}";

        return GetAsync<OperationalNotificationDto>(
            requestPath,
            cancellationToken);
    }

    private static string BuildListRequestPath(
        Guid? orderId,
        string? customerId,
        Guid? correlationId,
        int offset,
        int limit)
    {
        var queryParameters =
            new List<string>();

        if (orderId.HasValue)
        {
            queryParameters.Add(
                $"orderId={orderId.Value:D}");
        }

        if (!string.IsNullOrWhiteSpace(
                customerId))
        {
            string normalizedCustomerId =
                customerId.Trim();

            queryParameters.Add(
                $"customerId={Uri.EscapeDataString(normalizedCustomerId)}");
        }

        if (correlationId.HasValue)
        {
            queryParameters.Add(
                $"correlationId={correlationId.Value:D}");
        }

        queryParameters.Add(
            FormattableString.Invariant(
                $"offset={offset}"));

        queryParameters.Add(
            FormattableString.Invariant(
                $"limit={limit}"));

        return
            $"{OperationalNotificationsPath}?{string.Join("&", queryParameters)}";
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
                    $"Notifications response body for '{requestPath}' was empty.");
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
        EventId = 5600,
        Level = LogLevel.Warning,
        Message =
            "Notifications request {RequestPath} failed with HTTP {StatusCode}. " +
            "ErrorCode={ErrorCode} TraceId={TraceId} RequestId={RequestId}")]
    private static partial void LogHttpFailure(
        ILogger logger,
        string requestPath,
        int statusCode,
        string? errorCode,
        string? traceId,
        string? requestId);

    [LoggerMessage(
        EventId = 5601,
        Level = LogLevel.Warning,
        Message =
            "Notifications request {RequestPath} failed before receiving an HTTP response.")]
    private static partial void LogTransportFailure(
        ILogger logger,
        Exception exception,
        string requestPath);

    [LoggerMessage(
        EventId = 5602,
        Level = LogLevel.Warning,
        Message =
            "Notifications request {RequestPath} exceeded the configured timeout.")]
    private static partial void LogTimeout(
        ILogger logger,
        Exception exception,
        string requestPath);
}