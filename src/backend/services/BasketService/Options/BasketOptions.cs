namespace BasketService.Options;

public sealed class BasketOptions
{
    public const string SectionName = "Basket";

    public int ExpirationMinutes { get; init; } = 1_440;

    public int MaxQuantityPerItem { get; init; } = 100;
}
