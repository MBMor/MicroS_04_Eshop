using CatalogService.Domain;
using Xunit;

namespace Eshop.Domain.UnitTests.Catalog;

public sealed class ProductTests
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
    public void Create_ValidData_NormalizesValues()
    {
        Guid productId = Guid.NewGuid();

        Product product = Product.Create(
            productId,
            "  Mechanical Keyboard  ",
            "  sku-001  ",
            "  Gaming keyboard  ",
            "  Peripherals  ",
            priceAmount: 2_500m,
            currency: "  czk  ",
            isActive: true,
            CreatedAtUtc);

        Assert.Equal(productId, product.Id);
        Assert.Equal("Mechanical Keyboard", product.Name);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal("Gaming keyboard", product.Description);
        Assert.Equal("Peripherals", product.Category);
        Assert.Equal(2_500m, product.PriceAmount);
        Assert.Equal("CZK", product.Currency);
        Assert.True(product.IsActive);
        Assert.Equal(CreatedAtUtc, product.CreatedAtUtc);
        Assert.Null(product.UpdatedAtUtc);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Product.Create(
                Guid.Empty,
                "Keyboard",
                "SKU-001",
                description: null,
                "Peripherals",
                priceAmount: 2_500m,
                currency: "CZK",
                isActive: true,
                CreatedAtUtc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositivePrice_Throws(
        int priceAmount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Product.Create(
                Guid.NewGuid(),
                "Keyboard",
                "SKU-001",
                description: null,
                "Peripherals",
                priceAmount,
                currency: "CZK",
                isActive: true,
                CreatedAtUtc));
    }

    [Fact]
    public void Update_ValidData_UpdatesAndNormalizesValues()
    {
        Product product = CreateProduct();

        DateTimeOffset updatedAtUtc =
            CreatedAtUtc.AddMinutes(5);

        product.Update(
            "  Updated Keyboard  ",
            "  new-sku  ",
            "  Updated description  ",
            "  Updated category  ",
            priceAmount: 3_000m,
            currency: "  eur  ",
            isActive: false,
            updatedAtUtc);

        Assert.Equal("Updated Keyboard", product.Name);
        Assert.Equal("NEW-SKU", product.Sku);
        Assert.Equal(
            "Updated description",
            product.Description);

        Assert.Equal(
            "Updated category",
            product.Category);

        Assert.Equal(3_000m, product.PriceAmount);
        Assert.Equal("EUR", product.Currency);
        Assert.False(product.IsActive);
        Assert.Equal(updatedAtUtc, product.UpdatedAtUtc);
    }

    [Fact]
    public void Update_InvalidSku_ThrowsWithoutPartialMutation()
    {
        Product product = CreateProduct();

        Assert.Throws<ArgumentException>(
            () => product.Update(
                "Changed name",
                "   ",
                "Changed description",
                "Changed category",
                priceAmount: 3_000m,
                currency: "EUR",
                isActive: false,
                CreatedAtUtc.AddMinutes(5)));

        Assert.Equal("Keyboard", product.Name);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal("Description", product.Description);
        Assert.Equal("Peripherals", product.Category);
        Assert.Equal(2_500m, product.PriceAmount);
        Assert.Equal("CZK", product.Currency);
        Assert.True(product.IsActive);
        Assert.Null(product.UpdatedAtUtc);
    }

    [Fact]
    public void Deactivate_ActiveProduct_DeactivatesProduct()
    {
        Product product = CreateProduct();

        DateTimeOffset updatedAtUtc =
            CreatedAtUtc.AddMinutes(5);

        product.Deactivate(updatedAtUtc);

        Assert.False(product.IsActive);
        Assert.Equal(updatedAtUtc, product.UpdatedAtUtc);
    }

    private static Product CreateProduct()
    {
        return Product.Create(
            Guid.NewGuid(),
            "Keyboard",
            "SKU-001",
            "Description",
            "Peripherals",
            priceAmount: 2_500m,
            currency: "CZK",
            isActive: true,
            CreatedAtUtc);
    }
}
