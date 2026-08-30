using Eshop.Operations.Desktop.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.Api.Authentication;

public sealed class AccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsyncReturnsCurrentTokenWhenStillValid()
    {
        DateTimeOffset now =
            new(
                2026,
                8,
                29,
                12,
                0,
                0,
                TimeSpan.Zero);

        var refreshService =
            new StubTokenRefreshService();

        var provider =
            CreateProvider(
                refreshService,
                now);

        provider.StartSession(
            "access-1",
            "refresh-1",
            now.AddMinutes(5));

        string accessToken =
            await provider.GetAccessTokenAsync(
                CancellationToken.None);

        Assert.Equal(
            "access-1",
            accessToken);

        Assert.Equal(
            0,
            refreshService.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsyncRefreshesTokenNearExpiration()
    {
        DateTimeOffset now =
            new(
                2026,
                8,
                29,
                12,
                0,
                0,
                TimeSpan.Zero);

        var refreshService =
            new StubTokenRefreshService(
                TokenRefreshResult.Success(
                    "access-2",
                    "refresh-2",
                    now.AddMinutes(5)));

        var provider =
            CreateProvider(
                refreshService,
                now);

        provider.StartSession(
            "access-1",
            "refresh-1",
            now.AddSeconds(10));

        string accessToken =
            await provider.GetAccessTokenAsync(
                CancellationToken.None);

        Assert.Equal(
            "access-2",
            accessToken);

        Assert.Equal(
            1,
            refreshService.CallCount);
    }

    [Fact]
    public async Task ConcurrentAccessTokenRequestsShareSingleRefresh()
    {
        DateTimeOffset now =
            new(
                2026,
                8,
                29,
                12,
                0,
                0,
                TimeSpan.Zero);

        var refreshService =
            new BlockingTokenRefreshService();

        var provider =
            CreateProvider(
                refreshService,
                now);

        provider.StartSession(
            "access-1",
            "refresh-1",
            now.AddSeconds(10));

        Task<string> firstRequest =
            provider.GetAccessTokenAsync(
                CancellationToken.None);

        await refreshService.RefreshStarted;

        Task<string> secondRequest =
            provider.GetAccessTokenAsync(
                CancellationToken.None);

        refreshService.Complete(
            TokenRefreshResult.Success(
                "access-2",
                "refresh-2",
                now.AddMinutes(5)));

        string[] tokens =
            await Task.WhenAll(
                firstRequest,
                secondRequest);

        Assert.Equal(
            ["access-2", "access-2"],
            tokens);

        Assert.Equal(
            1,
            refreshService.CallCount);
    }

    private static AccessTokenProvider CreateProvider(
    ITokenRefreshService refreshService,
    DateTimeOffset utcNow)
    {
        return new AccessTokenProvider(
            refreshService,
            new AuthenticationState(),
            new StubTimeProvider(utcNow),
            NullLogger<AccessTokenProvider>.Instance);
    }

    private sealed class StubTimeProvider(
    DateTimeOffset utcNow)
    : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class StubTokenRefreshService
        : ITokenRefreshService
    {
        private readonly TokenRefreshResult? _result;

        public StubTokenRefreshService()
        {
        }

        public StubTokenRefreshService(
            TokenRefreshResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<TokenRefreshResult> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            CallCount++;

            if (_result is null)
            {
                throw new InvalidOperationException(
                    "Refresh was not expected.");
            }

            return Task.FromResult(
                _result);
        }
    }

    private sealed class BlockingTokenRefreshService
    : ITokenRefreshService
    {
        private readonly TaskCompletionSource _refreshStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<TokenRefreshResult> _result =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RefreshStarted =>
            _refreshStarted.Task;

        public int CallCount { get; private set; }

        public async Task<TokenRefreshResult> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            CallCount++;

            _refreshStarted.TrySetResult();

            return await _result.Task;
        }

        public void Complete(
            TokenRefreshResult result)
        {
            _result.TrySetResult(
                result);
        }
    }

}
