using Eshop.Operations.Desktop.Authentication;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.Authentication;

public sealed class AuthenticationStateTests
{
    [Fact]
    public void AdminCanAdjustInventory()
    {
        var state = new AuthenticationState(
            new AuthenticatedUser(
                "admin-123",
                "anna.admin",
                "anna.admin@eshop.local",
                ["admin"]));

        Assert.True(state.CanAccessOperations);
        Assert.True(state.CanAdjustInventory);
    }

    [Fact]
    public void SupportCannotAdjustInventory()
    {
        var state = new AuthenticationState(
            new AuthenticatedUser(
                "support-123",
                "sam.support",
                "sam.support@eshop.local",
                ["support"]));

        Assert.True(state.CanAccessOperations);
        Assert.False(state.CanAdjustInventory);
    }
}
