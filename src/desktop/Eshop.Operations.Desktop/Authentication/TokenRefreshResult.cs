namespace Eshop.Operations.Desktop.Authentication;

public sealed record TokenRefreshResult(
    bool Succeeded,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? AccessTokenExpiration,
    string? Error)
{
    public static TokenRefreshResult Success(
        string accessToken,
        string? refreshToken,
        DateTimeOffset accessTokenExpiration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            accessToken);

        return new TokenRefreshResult(
            true,
            accessToken,
            refreshToken,
            accessTokenExpiration,
            null);
    }

    public static TokenRefreshResult Failure(
        string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            error);

        return new TokenRefreshResult(
            false,
            null,
            null,
            null,
            error);
    }
}
