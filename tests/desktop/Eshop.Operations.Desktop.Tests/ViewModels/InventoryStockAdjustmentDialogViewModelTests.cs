using System.Globalization;

using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.Models;
using Eshop.Operations.Desktop.ViewModels;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class InventoryStockAdjustmentDialogViewModelTests
{
    [Fact]
    public void ValidAdjustmentCalculatesProjectedQuantities()
    {
        var viewModel = new InventoryStockAdjustmentDialogViewModel(CreateItem())
        {
            QuantityDeltaText = "-5",
            Reason = "Physical stock correction"
        };
        Assert.True(viewModel.CanApply);
        Assert.Equal(-5, viewModel.QuantityDelta);
        Assert.Equal(15, viewModel.ProjectedOnHandQuantity);
        Assert.Equal(10, viewModel.ProjectedAvailableQuantity);
        Assert.Null(viewModel.ValidationMessage);
    }

    [Fact]
    public void AdjustmentBelowReservedQuantityIsRejected()
    {
        var viewModel = new InventoryStockAdjustmentDialogViewModel(CreateItem())
        {
            QuantityDeltaText = "-16",
            Reason = "Physical stock correction"
        };
        Assert.False(viewModel.CanApply);
        Assert.NotNull(viewModel.ValidationMessage);
    }

    [Fact]
    public void ZeroAdjustmentIsRejected()
    {
        var viewModel = new InventoryStockAdjustmentDialogViewModel(CreateItem())
        {
            QuantityDeltaText = "0",
            Reason = "Physical stock correction"
        };
        Assert.False(viewModel.CanApply);
        Assert.Equal("Quantity delta must not be zero.", viewModel.ValidationMessage);
    }

    [Fact]
    public void TryCreateDraftTrimsReason()
    {
        var viewModel = new InventoryStockAdjustmentDialogViewModel(CreateItem())
        {
            QuantityDeltaText = "3",
            Reason = "  Physical count correction  "
        };
        Assert.True(viewModel.TryCreateDraft(out InventoryStockAdjustmentDraft? draft));
        Assert.NotNull(draft);
        Assert.Equal(3, draft.QuantityDelta);
        Assert.Equal("Physical count correction", draft.Reason);
    }

    private static InventoryItemDto CreateItem() => new(
        Guid.NewGuid(), Guid.NewGuid(), "KEY-001", 20, 5, 15, true,
        DateTimeOffset.Parse("2026-08-01T10:00:00+00:00", CultureInfo.InvariantCulture), null, 42);
}
