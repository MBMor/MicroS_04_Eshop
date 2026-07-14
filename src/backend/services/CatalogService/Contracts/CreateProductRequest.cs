using System.ComponentModel.DataAnnotations;

namespace CatalogService.Contracts;

public sealed class CreateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string Sku { get; init; }

    [StringLength(2_000)]
    public string? Description { get; init; }

    [Required]
    [StringLength(120, MinimumLength = 1)]
    public required string Category { get; init; }

    [Range(0.01, 999_999_999.99)]
    public decimal PriceAmount { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "CZK";

    public bool IsActive { get; init; } = true;
}
