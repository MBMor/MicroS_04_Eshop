using Duende.IdentityModel.OidcClient;
using Eshop.Operations.Desktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eshop.Operations.Desktop.Authentication;

public sealed class OidcTokenRefreshService
    : ITokenRefreshService
{
    private static readonly TimeSpan BackchannelTimeout =
        TimeSpan.FromSeconds(15);

    private const string RefreshRedirectUri =
        "http://127.0.0.1/";

    private readonly OidcClient _oidcClient;

    public OidcTokenRefreshService(
        IOptions<AuthenticationOptions> options,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        AuthenticationOptions authenticationOptions =
            options.Value;

        var oidcOptions =
            new OidcClientOptions
            {
                Authority =
                    authenticationOptions.Authority,

                ClientId =
                    authenticationOptions.ClientId,

                Scope =
                    authenticationOptions.Scope,

                RedirectUri =
                    RefreshRedirectUri,

                LoadProfile =
                    false,

                DisablePushedAuthorization =
                    true,

                BackchannelTimeout =
                    BackchannelTimeout,

                LoggerFactory =
                    loggerFactory
            };

        _oidcClient =
            new OidcClient(
                oidcOptions);
    }

    public async Task<TokenRefreshResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            refreshToken);

        var result =
            await _oidcClient.RefreshTokenAsync(
                refreshToken,
                cancellationToken:
                    cancellationToken);

        if (result.IsError
            || string.IsNullOrWhiteSpace(
                result.AccessToken))
        {
            string error =
                result.Error
                ?? result.ErrorDescription
                ?? "OIDC token refresh failed.";

            return TokenRefreshResult.Failure(
                error);
        }

        return TokenRefreshResult.Success(
            result.AccessToken,
            result.RefreshToken,
            result.AccessTokenExpiration);
    }
}
