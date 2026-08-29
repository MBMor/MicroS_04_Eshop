namespace Eshop.Operations.Desktop.Authentication;

public interface IAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken);

    void InvalidateSession();
}
