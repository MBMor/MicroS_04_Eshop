using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Duende.IdentityModel.OidcClient;
using Eshop.Operations.Desktop.Api.Authentication;
using Eshop.Operations.Desktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eshop.Operations.Desktop.Authentication;

public sealed partial class AuthenticationService
    : IAuthenticationService
{
    private const string CallbackResponseHtml =
        """
        <!doctype html>
        <html>
        <head>
            <meta charset="utf-8">
            <title>Eshop Operations Console</title>
        </head>
        <body>
            Authentication completed. You can return to Eshop Operations Console.
        </body>
        </html>
        """;

    private readonly AuthenticationOptions _options;
    private readonly AuthenticationState _state;
    private readonly CurrentUserApiClient _currentUserApiClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AuthenticationService> _logger;

    private const string OidcBackchannelHttpClientName =
        "OidcBackchannel";

    private readonly AccessTokenProvider _accessTokenProvider;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthenticationService(
        IOptions<AuthenticationOptions> options,
        AuthenticationState state,
        AccessTokenProvider accessTokenProvider,
        CurrentUserApiClient currentUserApiClient,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        ILogger<AuthenticationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(currentUserApiClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _options = options.Value;
        _state = state;
        _currentUserApiClient = currentUserApiClient;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _accessTokenProvider = accessTokenProvider;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AuthenticationOperationResult> SignInAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            int port =
                GetAvailableLoopbackPort();

            string redirectUri =
                $"http://127.0.0.1:{port}/";

            using var listener =
                new HttpListener();

            listener.Prefixes.Add(
                redirectUri);

            listener.Start();

            OidcClient oidcClient =
                CreateOidcClient(
                    redirectUri);

            var authorizeState =
                await oidcClient.PrepareLoginAsync(
                    cancellationToken: cancellationToken);

            if (authorizeState.IsError)
            {
                LogOidcSignInFailure(
                    _logger,
                    authorizeState.Error);

                return AuthenticationOperationResult.Failure(
                    "The authentication request could not be prepared.");
            }

            if (string.IsNullOrWhiteSpace(
                    authorizeState.StartUrl))
            {
                throw new InvalidOperationException(
                    "The OIDC authorization request did not contain a start URL.");
            }

            using Process? browserProcess =
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName =
                            authorizeState.StartUrl,
                        UseShellExecute = true
                    });

            if (browserProcess is null)
            {
                return AuthenticationOperationResult.Failure(
                    "The system browser could not be opened.");
            }

            using var timeoutSource =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(
                        _options.CallbackTimeoutSeconds));

            using var linkedSource =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken,
                        timeoutSource.Token);

            HttpListenerContext context =
                await listener
                    .GetContextAsync()
                    .WaitAsync(
                        linkedSource.Token);

            await SendBrowserResponseAsync(
                context.Response,
                cancellationToken);

            string responseUrl =
                context.Request.RawUrl
                ?? throw new InvalidOperationException(
                    "The authentication callback did not contain a response URL.");

            var loginResult =
                await oidcClient.ProcessResponseAsync(
                    responseUrl,
                    authorizeState,
                    cancellationToken:
                        cancellationToken);

            if (loginResult.IsError
                || string.IsNullOrWhiteSpace(
                    loginResult.AccessToken))
            {
                LogOidcSignInFailure(
                    _logger,
                    loginResult.Error);

                return AuthenticationOperationResult.Failure(
                    "Authentication was not completed.");
            }

            _accessTokenProvider.StartSession(
                loginResult.AccessToken,
                loginResult.RefreshToken,
                loginResult.AccessTokenExpiration);

            AuthenticatedUser user =
                await _currentUserApiClient
                    .GetCurrentUserAsync(
                        cancellationToken);

            _state.SetAuthenticatedUser(
                user);

            LogSignInSucceeded(
                _logger,
                user.Subject);

            return AuthenticationOperationResult.Success();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return AuthenticationOperationResult.Failure(
                "Authentication was canceled.");
        }
        catch (OperationCanceledException)
        {
            return AuthenticationOperationResult.Failure(
                "Authentication timed out.");
        }
        catch (Exception exception)
        {
            LogSignInFailed(
                _logger,
                exception);

            ClearSession();

            return AuthenticationOperationResult.Failure(
                "Authentication failed. Check that Keycloak and the API Gateway are available.");
        }
    }

    public async Task<AuthenticationOperationResult> SignOutAsync(
        CancellationToken cancellationToken)
    {
        string? refreshToken =
            _accessTokenProvider.CurrentRefreshToken;

        try
        {
            if (string.IsNullOrWhiteSpace(
                    refreshToken))
            {
                return AuthenticationOperationResult.Success();
            }

            using HttpClient httpClient =
                _httpClientFactory.CreateClient(
                    OidcBackchannelHttpClientName);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "protocol/openid-connect/revoke");

            request.Content =
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["client_id"] =
                            _options.ClientId,

                        ["token"] =
                            refreshToken,

                        ["token_type_hint"] =
                            "refresh_token"
                    });

            using HttpResponseMessage response =
                await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                LogTokenRevocationFailed(
                    _logger,
                    (int)response.StatusCode);

                return AuthenticationOperationResult.Failure(
                    $"Signed out locally, but server token revocation failed with HTTP {(int)response.StatusCode}.");
            }

            return AuthenticationOperationResult.Success();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return AuthenticationOperationResult.Failure(
                "Signed out locally, but server token revocation was canceled.");
        }
        catch (Exception exception)
        {
            LogTokenRevocationException(
                _logger,
                exception);

            return AuthenticationOperationResult.Failure(
                "Signed out locally, but server token revocation failed.");
        }
        finally
        {
            ClearSession();

            LogSignedOut(
                _logger);
        }
    }

    private OidcClient CreateOidcClient(
        string redirectUri)
    {
        var options =
            new OidcClientOptions
            {
                Authority =
                    _options.Authority,

                ClientId =
                    _options.ClientId,

                Scope =
                    _options.Scope,

                RedirectUri =
                    redirectUri,

                LoadProfile =
                    false,

                DisablePushedAuthorization = true,

                LoggerFactory =
                    _loggerFactory
            };

        return new OidcClient(
            options);
    }

    private void ClearSession()
    {
        _accessTokenProvider
            .InvalidateSession();
    }

    private static int GetAvailableLoopbackPort()
    {
        var listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        try
        {
            listener.Start();

            return ((IPEndPoint)
                listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task SendBrowserResponseAsync(
        HttpListenerResponse response,
        CancellationToken cancellationToken)
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                CallbackResponseHtml);

        response.ContentType =
            "text/html; charset=utf-8";

        response.ContentLength64 =
            content.Length;

        await response.OutputStream.WriteAsync(
            content,
            cancellationToken);

        response.OutputStream.Close();
    }

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Information,
        Message = "Desktop authentication succeeded for subject {Subject}.")]
    private static partial void LogSignInSucceeded(
        ILogger logger,
        string subject);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "OIDC authentication failed: {Error}")]
    private static partial void LogOidcSignInFailure(
        ILogger logger,
        string? error);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Error,
        Message = "Desktop authentication failed.")]
    private static partial void LogSignInFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Information,
        Message = "Desktop authentication session was cleared.")]
    private static partial void LogSignedOut(
        ILogger logger);

    [LoggerMessage(
    EventId = 4004,
    Level = LogLevel.Warning,
    Message =
        "Refresh-token revocation failed with HTTP {StatusCode}.")]
    private static partial void LogTokenRevocationFailed(
    ILogger logger,
    int statusCode);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Warning,
        Message =
            "Refresh-token revocation failed.")]
    private static partial void LogTokenRevocationException(
        ILogger logger,
        Exception exception);
}
