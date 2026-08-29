using System.Net;
using System.Text;
using Eshop.Operations.Desktop.Api.Authentication;
using Eshop.Operations.Desktop.Authentication;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.Authentication;

public sealed class CurrentUserApiClientTests
{
    [Fact]
    public async Task GetCurrentUserAsyncSendsBearerTokenAndMapsResponse()
    {
        const string json =
            """
            {
              "subject": "user-123",
              "preferredUsername": "sam.support",
              "email": "sam.support@eshop.local",
              "roles": ["support"]
            }
            """;

        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Equal(
                    HttpMethod.Get,
                    request.Method);

                Assert.Equal(
                    "Bearer",
                    request.Headers.Authorization?.Scheme);

                Assert.Equal(
                    "access-token",
                    request.Headers.Authorization?.Parameter);

                Assert.Equal(
                    "http://localhost:5080/api/v1/auth/me",
                    request.RequestUri?.ToString());

                return new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json")
                };
            });

        var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "http://localhost:5080/",
                        UriKind.Absolute)
            };

        var client =
            new CurrentUserApiClient(
                new StubHttpClientFactory(
                    httpClient));

        AuthenticatedUser user =
            await client.GetCurrentUserAsync(
                "access-token",
                CancellationToken.None);

        Assert.Equal(
            "user-123",
            user.Subject);

        Assert.Equal(
            "sam.support",
            user.PreferredUsername);

        Assert.Equal(
            "sam.support@eshop.local",
            user.Email);

        Assert.Equal(
            ["support"],
            user.Roles);
    }

    private sealed class StubHttpClientFactory(
        HttpClient httpClient)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            return httpClient;
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
