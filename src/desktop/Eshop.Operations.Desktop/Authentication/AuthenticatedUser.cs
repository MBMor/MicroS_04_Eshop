namespace Eshop.Operations.Desktop.Authentication;

public sealed record AuthenticatedUser(
    string Subject,
    string? PreferredUsername,
    string? Email,
    string[] Roles)
{
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(PreferredUsername)
            ? PreferredUsername
            : !string.IsNullOrWhiteSpace(Email)
                ? Email
                : Subject;

    public string RolesDisplay =>
        Roles.Length == 0
            ? "No application role"
            : string.Join(", ", Roles);
}
