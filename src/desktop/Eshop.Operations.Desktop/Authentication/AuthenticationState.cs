using CommunityToolkit.Mvvm.ComponentModel;

namespace Eshop.Operations.Desktop.Authentication;

public sealed partial class AuthenticationState : ObservableObject
{
    public AuthenticationState(
        AuthenticatedUser? currentUser = null)
    {
        CurrentUser = currentUser;
    }

    public bool IsAuthenticated =>
        CurrentUser is not null;

    public bool IsAnonymous =>
        CurrentUser is null;

    public bool CanAccessOperations =>
        CurrentUser?.Roles.Contains(
            "support",
            StringComparer.Ordinal) == true
        || CurrentUser?.Roles.Contains(
            "admin",
            StringComparer.Ordinal) == true;

    public bool CanAdjustInventory =>
        CurrentUser?.Roles.Contains(
            "admin",
            StringComparer.Ordinal) == true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAuthenticated))]
    [NotifyPropertyChangedFor(nameof(IsAnonymous))]
    [NotifyPropertyChangedFor(nameof(CanAccessOperations))]
    [NotifyPropertyChangedFor(nameof(CanAdjustInventory))]
    public partial AuthenticatedUser? CurrentUser
    {
        get;
        private set;
    }

    internal void SetAuthenticatedUser(
        AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        CurrentUser = user;
    }

    internal void Clear()
    {
        CurrentUser = null;
    }
}
