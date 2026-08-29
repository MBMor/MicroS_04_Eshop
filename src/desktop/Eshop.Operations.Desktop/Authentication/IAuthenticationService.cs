namespace Eshop.Operations.Desktop.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationOperationResult> SignInAsync(
        CancellationToken cancellationToken);

    Task<AuthenticationOperationResult> SignOutAsync(
        CancellationToken cancellationToken);
}
