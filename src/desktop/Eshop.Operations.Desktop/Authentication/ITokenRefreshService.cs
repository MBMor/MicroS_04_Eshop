namespace Eshop.Operations.Desktop.Authentication;

public interface ITokenRefreshService
{
    Task<TokenRefreshResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);
}
