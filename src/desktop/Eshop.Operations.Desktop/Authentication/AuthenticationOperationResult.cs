namespace Eshop.Operations.Desktop.Authentication;

public sealed record AuthenticationOperationResult(
    bool Succeeded,
    string? ErrorMessage)
{
    public static AuthenticationOperationResult Success() =>
        new(
            true,
            null);

    public static AuthenticationOperationResult Failure(
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            errorMessage);

        return new AuthenticationOperationResult(
            false,
            errorMessage);
    }
}
