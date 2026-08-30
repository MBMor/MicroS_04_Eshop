using System.Net;
using Eshop.Operations.Desktop.Api.Authentication;
using Eshop.Operations.Desktop.Authentication;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.Authentication;

public sealed class AuthenticationDelegatingHandlerTests
{
    [Fact]
    public async Task UnauthorizedResponseInvalidatesAuthenticationSession()
    {
        var accessTokenProvider =
            new StubAccessTokenProvider();

        var terminalHandler =
            new StubHttpMessageHandler(
                (_, _) =>
                    new HttpResponseMessage(
                        HttpStatusCode.Unauthorized));

        var authenticationHandler =
            new AuthenticationDelegatingHandler(
                accessTokenProvider)
            {
                InnerHandler =
                    terminalHandler
            };

        using var httpClient =
            new HttpClient(
                authenticationHandler);

        using HttpResponseMessage response =
            await httpClient.GetAsync(
                "http://localhost/protected",
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        Assert.True(
            accessTokenProvider.SessionInvalidated);
    }

    private sealed class StubAccessTokenProvider
        : IAccessTokenProvider
    {
        public bool SessionInvalidated { get; private set; }

        public Task<string> GetAccessTokenAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                "access-token");
        }

        public void InvalidateSession()
        {
            SessionInvalidated =
                true;
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                send(
                    request,
                    cancellationToken));
        }
    }
}
