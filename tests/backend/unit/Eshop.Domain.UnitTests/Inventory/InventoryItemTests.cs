using InventoryService.Domain;
using Xunit;

namespace Eshop.Domain.UnitTests.Inventory;

public sealed class InventoryItemTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            year: 2026,
            month: 7,
            day: 23,
            hour: 8,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

    [Fact]
    public void Create_ValidData_NormalizesSkuAndCalculatesAvailability()
    {
        InventoryItem item = CreateItem();

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.NotEqual(Guid.Empty, item.ProductId);
        Assert.Equal("SKU-001", item.Sku);
        Assert.Equal(10, item.OnHandQuantity);
        Assert.Equal(0, item.ReservedQuantity);
        Assert.Equal(10, item.AvailableQuantity);
        Assert.True(item.IsActive);
        Assert.Equal(CreatedAtUtc, item.CreatedAtUtc);
        Assert.Null(item.UpdatedAtUtc);
    }

    [Fact]
    public void TryReserve_ActiveItemWithAvailableStock_ReservesQuantity()
    {
        InventoryItem item = CreateItem();
        DateTimeOffset updatedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        bool reserved = item.TryReserve(
            quantity: 4,
            updatedAtUtc);

        Assert.True(reserved);
        Assert.Equal(10, item.OnHandQuantity);
        Assert.Equal(4, item.ReservedQuantity);
        Assert.Equal(6, item.AvailableQuantity);
        Assert.Equal(updatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void TryReserve_InactiveItem_ReturnsFalseWithoutMutation()
    {
        InventoryItem item = CreateItem(
            isActive: false);

        bool reserved = item.TryReserve(
            quantity: 1,
            CreatedAtUtc.AddMinutes(1));

        Assert.False(reserved);
        Assert.Equal(0, item.ReservedQuantity);
        Assert.Equal(10, item.AvailableQuantity);
        Assert.Null(item.UpdatedAtUtc);
    }

    [Fact]
    public void TryReserve_InsufficientAvailableStock_ReturnsFalse()
    {
        InventoryItem item = CreateItem();

        bool reserved = item.TryReserve(
            quantity: 11,
            CreatedAtUtc.AddMinutes(1));

        Assert.False(reserved);
        Assert.Equal(0, item.ReservedQuantity);
        Assert.Equal(10, item.AvailableQuantity);
    }

    [Fact]
    public void Update_OnHandBelowReservedQuantity_ThrowsWithoutMutation()
    {
        InventoryItem item = CreateItem();

        Assert.True(
            item.TryReserve(
                quantity: 6,
                CreatedAtUtc.AddMinutes(1)));

        Assert.Throws<InvalidOperationException>(
            () => item.Update(
                "SKU-UPDATED",
                onHandQuantity: 5,
                isActive: true,
                CreatedAtUtc.AddMinutes(2)));

        Assert.Equal("SKU-001", item.Sku);
        Assert.Equal(10, item.OnHandQuantity);
        Assert.Equal(6, item.ReservedQuantity);
    }

    [Fact]
    public void AdjustOnHandQuantity_ResultBelowReservedQuantity_Throws()
    {
        InventoryItem item = CreateItem();

        Assert.True(
            item.TryReserve(
                quantity: 6,
                CreatedAtUtc.AddMinutes(1)));

        Assert.Throws<InvalidOperationException>(
            () => item.AdjustOnHandQuantity(
                quantityDelta: -5,
                CreatedAtUtc.AddMinutes(2)));

        Assert.Equal(10, item.OnHandQuantity);
        Assert.Equal(6, item.ReservedQuantity);
    }

    [Fact]
    public void AdjustOnHandQuantity_ValidIncrease_UpdatesAvailability()
    {
        InventoryItem item = CreateItem();
        DateTimeOffset updatedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        item.AdjustOnHandQuantity(
            quantityDelta: 5,
            updatedAtUtc);

        Assert.Equal(15, item.OnHandQuantity);
        Assert.Equal(0, item.ReservedQuantity);
        Assert.Equal(15, item.AvailableQuantity);
        Assert.Equal(updatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void ReleaseReservation_ReservedStock_ReturnsItToAvailability()
    {
        InventoryItem item = CreateItem();

        Assert.True(
            item.TryReserve(
                quantity: 4,
                CreatedAtUtc.AddMinutes(1)));

        DateTimeOffset releasedAtUtc =
            CreatedAtUtc.AddMinutes(2);

        item.ReleaseReservation(
            quantity: 3,
            releasedAtUtc);

        Assert.Equal(10, item.OnHandQuantity);
        Assert.Equal(1, item.ReservedQuantity);
        Assert.Equal(9, item.AvailableQuantity);
        Assert.Equal(releasedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void CommitReservation_ReservedStock_DecreasesOnHandQuantity()
    {
        InventoryItem item = CreateItem();

        Assert.True(
            item.TryReserve(
                quantity: 4,
                CreatedAtUtc.AddMinutes(1)));

        DateTimeOffset committedAtUtc =
            CreatedAtUtc.AddMinutes(2);

        item.CommitReservation(
            quantity: 4,
            committedAtUtc);

        Assert.Equal(6, item.OnHandQuantity);
        Assert.Equal(0, item.ReservedQuantity);
        Assert.Equal(6, item.AvailableQuantity);
        Assert.Equal(committedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void ReleaseReservation_MoreThanReserved_ThrowsWithoutMutation()
    {
        InventoryItem item = CreateItem();

        Assert.True(
            item.TryReserve(
                quantity: 2,
                CreatedAtUtc.AddMinutes(1)));

        Assert.Throws<InvalidOperationException>(
            () => item.ReleaseReservation(
                quantity: 3,
                CreatedAtUtc.AddMinutes(2)));

        Assert.Equal(10, item.OnHandQuantity);
        Assert.Equal(2, item.ReservedQuantity);
        Assert.Equal(8, item.AvailableQuantity);
    }

    private static InventoryItem CreateItem(
        bool isActive = true)
    {
        return InventoryItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  sku-001  ",
            initialOnHandQuantity: 10,
            isActive,
            CreatedAtUtc);
    }
}
