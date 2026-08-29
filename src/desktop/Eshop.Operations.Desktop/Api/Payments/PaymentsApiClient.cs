using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.Api.Payments;

public sealed partial class PaymentsApiClient
    : IPaymentsApiClient
{
    private const string HttpClientName =
        "ApiGatewayAuthenticated";

    private const string PaymentsPath =
        "api/v1/payments";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaymentsApiClient> _logger;

    public PaymentsApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<PaymentsApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient =
            _httpClientFactory.CreateClient(HttpClientName);

        using var request =
            new HttpRequestMessage(HttpMethod.Get, PaymentsPath);

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
                    (int)response.StatusCode,
                    problemDetails?.ErrorCode,
                    problemDetails?.TraceId,
                    problemDetails?.RequestId);

                throw new ApiRequestException(
                    response.StatusCode,
                    problemDetails);
            }

            PaymentDto[]? payments =
                await response.Content
                    .ReadFromJsonAsync<PaymentDto[]>(
                        JsonOptions,
                        cancellationToken);

            return payments
                ?? throw new JsonException(
                    "Payments response body was empty.");
        }
        catch (HttpRequestException exception)
        {
            LogTransportFailure(_logger, exception);
            throw;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(_logger, exception);
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
        EventId = 5200,
        Level = LogLevel.Warning,
        Message =
            "Payments request failed with HTTP {StatusCode}. " +
            "ErrorCode={ErrorCode} TraceId={TraceId} RequestId={RequestId}")]
    private static partial void LogHttpFailure(
        ILogger logger,
        int statusCode,
        string? errorCode,
        string? traceId,
        string? requestId);

    [LoggerMessage(
        EventId = 5201,
        Level = LogLevel.Warning,
        Message =
            "Payments request failed before receiving an HTTP response.")]
    private static partial void LogTransportFailure(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 5202,
        Level = LogLevel.Warning,
        Message =
            "Payments request exceeded the configured timeout.")]
    private static partial void LogTimeout(
        ILogger logger,
        Exception exception);
}
