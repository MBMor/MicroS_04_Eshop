namespace Eshop.Operations.Desktop.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationOperationResult> SignInAsync(
        CancellationToken cancellationToken);

    void SignOut();
}
