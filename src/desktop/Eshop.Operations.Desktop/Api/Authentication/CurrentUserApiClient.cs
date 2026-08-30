using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Eshop.Operations.Desktop.Authentication;

namespace Eshop.Operations.Desktop.Api.Authentication;

public sealed class CurrentUserApiClient
{
    private const string HttpClientName = "ApiGatewayAuthenticated";

    private const string CurrentUserPath = "api/v1/auth/me";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;

    public CurrentUserApiClient(
        IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(
            httpClientFactory);

        _httpClientFactory =
            httpClientFactory;
    }

    public async Task<AuthenticatedUser> GetCurrentUserAsync(
    CancellationToken cancellationToken)
    {

        using HttpClient httpClient =
            _httpClientFactory.CreateClient(
                HttpClientName);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                CurrentUserPath);

        using HttpResponseMessage response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        AuthenticatedUser? user =
            await response.Content
                .ReadFromJsonAsync<AuthenticatedUser>(
                    JsonOptions,
                    cancellationToken);

        if (user is null
            || string.IsNullOrWhiteSpace(user.Subject))
        {
            throw new JsonException(
                "The current-user response did not contain a valid subject.");
        }

        return user;
    }
}
