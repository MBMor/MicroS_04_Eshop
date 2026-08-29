using Microsoft.Extensions.Logging;

namespace Eshop.Operations.Desktop.Authentication;

public sealed partial class AccessTokenProvider
    : IAccessTokenProvider
{
    private static readonly TimeSpan RefreshSkew =
        TimeSpan.FromSeconds(30);

    private readonly object _sync = new();

    private readonly ITokenRefreshService _refreshService;
    private readonly AuthenticationState _authenticationState;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccessTokenProvider> _logger;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset? _accessTokenExpiration;

    private Task<string>? _refreshTask;
    private long _sessionVersion;

    public AccessTokenProvider(
        ITokenRefreshService refreshService,
        AuthenticationState authenticationState,
        TimeProvider timeProvider,
        ILogger<AccessTokenProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(refreshService);
        ArgumentNullException.ThrowIfNull(authenticationState);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _refreshService = refreshService;
        _authenticationState = authenticationState;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    internal string? CurrentRefreshToken
    {
        get
        {
            lock (_sync)
            {
                return _refreshToken;
            }
        }
    }

    public void StartSession(
        string accessToken,
        string? refreshToken,
        DateTimeOffset accessTokenExpiration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            accessToken);

        lock (_sync)
        {
            _sessionVersion++;

            _accessToken =
                accessToken;

            _refreshToken =
                refreshToken;

            _accessTokenExpiration =
                accessTokenExpiration;

            _refreshTask =
                null;
        }
    }

    public Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken)
    {
        Task<string>? refreshTask;

        lock (_sync)
        {
            if (TryGetUsableAccessTokenLocked(
                    out string accessToken))
            {
                return Task.FromResult(
                    accessToken);
            }

            if (string.IsNullOrWhiteSpace(
                    _refreshToken))
            {
                refreshTask =
                    null;
            }
            else
            {
                _refreshTask ??=
                    RefreshAccessTokenCoreAsync(
                        _sessionVersion,
                        _refreshToken);

                refreshTask =
                    _refreshTask;
            }
        }

        if (refreshTask is null)
        {
            InvalidateSession();

            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        return AwaitRefreshAsync(
            refreshTask,
            cancellationToken);
    }

    public void InvalidateSession()
    {
        lock (_sync)
        {
            _sessionVersion++;

            _accessToken = null;
            _refreshToken = null;
            _accessTokenExpiration = null;

            _refreshTask = null;
        }

        _authenticationState.Clear();
    }

    private bool TryGetUsableAccessTokenLocked(
        out string accessToken)
    {
        accessToken =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                _accessToken)
            || _accessTokenExpiration is null)
        {
            return false;
        }

        DateTimeOffset refreshThreshold =
            _timeProvider
                .GetUtcNow()
                .Add(
                    RefreshSkew);

        if (_accessTokenExpiration <= refreshThreshold)
        {
            return false;
        }

        accessToken =
            _accessToken;

        return true;
    }

    private async Task<string> RefreshAccessTokenCoreAsync(
        long sessionVersion,
        string refreshToken)
    {
        TokenRefreshResult result =
            await _refreshService.RefreshAsync(
                refreshToken,
                CancellationToken.None);

        if (!result.Succeeded
            || string.IsNullOrWhiteSpace(
                result.AccessToken)
            || result.AccessTokenExpiration is null)
        {
            LogAccessTokenRefreshFailed(
                _logger,
                result.Error);

            InvalidateSession();

            throw new UnauthorizedAccessException(
                "The authentication session expired.");
        }

        lock (_sync)
        {
            if (_sessionVersion != sessionVersion)
            {
                throw new UnauthorizedAccessException(
                    "The authentication session changed while the access token was refreshing.");
            }

            _accessToken =
                result.AccessToken;

            _refreshToken =
                string.IsNullOrWhiteSpace(
                    result.RefreshToken)
                    ? refreshToken
                    : result.RefreshToken;

            _accessTokenExpiration =
                result.AccessTokenExpiration.Value;
        }

        LogAccessTokenRefreshed(
            _logger);

        return result.AccessToken;
    }

    private async Task<string> AwaitRefreshAsync(
        Task<string> refreshTask,
        CancellationToken cancellationToken)
    {
        try
        {
            return await refreshTask.WaitAsync(
                cancellationToken);
        }
        finally
        {
            if (refreshTask.IsCompleted)
            {
                lock (_sync)
                {
                    if (ReferenceEquals(
                            _refreshTask,
                            refreshTask))
                    {
                        _refreshTask =
                            null;
                    }
                }
            }
        }
    }

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message = "OIDC access token was refreshed.")]
    private static partial void LogAccessTokenRefreshed(
        ILogger logger);

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "OIDC access-token refresh failed: {Error}")]
    private static partial void LogAccessTokenRefreshFailed(
        ILogger logger,
        string? error);
}
