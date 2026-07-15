using System.ComponentModel.DataAnnotations;

namespace InventoryService.Contracts;

public sealed class UpdateInventoryItemRequest
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string Sku { get; init; }

    [Range(0, int.MaxValue)]
    public int OnHandQuantity { get; init; }

    public bool IsActive { get; init; } = true;
}
