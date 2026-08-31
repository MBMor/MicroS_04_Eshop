using Eshop.Operations.Desktop.Navigation;
using Eshop.Operations.Desktop.ViewModels;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class InvestigationViewModelTests
{
    [Fact]
    public void TryGetLookupRejectsInvalidGuid()
    {
        var viewModel = new InvestigationViewModel
        {
            IdentifierText = "not-a-guid"
        };

        bool result = viewModel.TryGetLookup(out _, out _);

        Assert.False(result);
        Assert.Equal(
            "Enter a valid non-empty GUID.",
            viewModel.ErrorMessage);
    }

    [Fact]
    public void TryGetLookupReturnsSelectedKindAndIdentifier()
    {
        Guid identifier = Guid.NewGuid();
        var viewModel = new InvestigationViewModel
        {
            IdentifierText = identifier.ToString("D")
        };

        viewModel.SelectedLookupOption = viewModel.LookupOptions.Single(
            option => option.Kind == OperationalLookupKind.PaymentsForOrder);

        bool result = viewModel.TryGetLookup(
            out OperationalLookupKind kind,
            out Guid parsedIdentifier);

        Assert.True(result);
        Assert.Equal(OperationalLookupKind.PaymentsForOrder, kind);
        Assert.Equal(identifier, parsedIdentifier);
    }
}
