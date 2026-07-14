using CatalogService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureProduct(modelBuilder.Entity<Product>());
    }

    private static void ConfigureProduct(EntityTypeBuilder<Product> product)
    {
        product.ToTable("products");

        product.HasKey(p => p.Id);

        product.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        product.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        product.Property(p => p.Sku)
            .HasColumnName("sku")
            .HasMaxLength(64)
            .IsRequired();

        product.HasIndex(p => p.Sku)
            .IsUnique();

        product.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(2_000)
            .IsRequired();

        product.Property(p => p.Category)
            .HasColumnName("category")
            .HasMaxLength(120)
            .IsRequired();

        product.Property(p => p.PriceAmount)
            .HasColumnName("price_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        product.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        product.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        product.Property(p => p.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        product.Property(p => p.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");
    }
}
