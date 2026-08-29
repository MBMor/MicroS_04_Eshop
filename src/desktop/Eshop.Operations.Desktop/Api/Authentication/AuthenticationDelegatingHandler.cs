using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Eshop.Operations.Desktop.Authentication;

namespace Eshop.Operations.Desktop.Api.Authentication;

public sealed class AuthenticationDelegatingHandler
    : DelegatingHandler
{
    private readonly IAccessTokenProvider _accessTokenProvider;

    public AuthenticationDelegatingHandler(
        IAccessTokenProvider accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(
            accessTokenProvider);

        _accessTokenProvider =
            accessTokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string accessToken =
            await _accessTokenProvider
                .GetAccessTokenAsync(
                    cancellationToken);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        HttpResponseMessage response =
            await base.SendAsync(
                request,
                cancellationToken);

        if (response.StatusCode
            == HttpStatusCode.Unauthorized)
        {
            _accessTokenProvider
                .InvalidateSession();
        }

        return response;
    }
}
