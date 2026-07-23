namespace CatalogService.Domain;

public sealed class Product
{
    private Product()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public decimal PriceAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Product Create(
        Guid id,
        string name,
        string sku,
        string? description,
        string category,
        decimal priceAmount,
        string currency,
        bool isActive,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product id must not be empty.", nameof(id));
        }

        ValidatePrice(priceAmount);

        return new Product
        {
            Id = id,
            Name = RequiredTrimmed(name, nameof(name)),
            Sku = RequiredTrimmed(sku, nameof(sku)).ToUpperInvariant(),
            Description = description?.Trim() ?? string.Empty,
            Category = RequiredTrimmed(category, nameof(category)),
            PriceAmount = priceAmount,
            Currency = RequiredTrimmed(currency, nameof(currency)).ToUpperInvariant(),
            IsActive = isActive,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = null
        };
    }

    public void Update(
        string name,
        string sku,
        string? description,
        string category,
        decimal priceAmount,
        string currency,
        bool isActive,
        DateTimeOffset updatedAtUtc)
    {
        ValidatePrice(priceAmount);

        string normalizedName =
            RequiredTrimmed(name, nameof(name));

        string normalizedSku =
            RequiredTrimmed(sku, nameof(sku))
                .ToUpperInvariant();

        string normalizedDescription =
            description?.Trim() ?? string.Empty;

        string normalizedCategory =
            RequiredTrimmed(category, nameof(category));

        string normalizedCurrency =
            RequiredTrimmed(currency, nameof(currency))
                .ToUpperInvariant();

        Name = normalizedName;
        Sku = normalizedSku;
        Description = normalizedDescription;
        Category = normalizedCategory;
        PriceAmount = priceAmount;
        Currency = normalizedCurrency;
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        IsActive = false;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string RequiredTrimmed(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }

    private static void ValidatePrice(decimal priceAmount)
    {
        if (priceAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceAmount), priceAmount, "Price must be greater than zero.");
        }
    }
}
