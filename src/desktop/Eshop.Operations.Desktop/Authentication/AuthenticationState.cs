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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAuthenticated))]
    [NotifyPropertyChangedFor(nameof(IsAnonymous))]
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
