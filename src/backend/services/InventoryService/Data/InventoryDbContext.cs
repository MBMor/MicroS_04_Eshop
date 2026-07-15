using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Data;

public sealed class InventoryDbContext(
    DbContextOptions<InventoryDbContext> options)
    : DbContext(options)
{
    public DbSet<InventoryItem> InventoryItems =>
        Set<InventoryItem>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureInventoryItem(
            modelBuilder.Entity<InventoryItem>());
    }

    private static void ConfigureInventoryItem(
        EntityTypeBuilder<InventoryItem> inventoryItem)
    {
        inventoryItem.ToTable(
            "inventory_items",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_inventory_items_on_hand_non_negative",
                    "\"on_hand_quantity\" >= 0");

                tableBuilder.HasCheckConstraint(
                    "ck_inventory_items_reserved_non_negative",
                    "\"reserved_quantity\" >= 0");

                tableBuilder.HasCheckConstraint(
                    "ck_inventory_items_reserved_not_above_on_hand",
                    "\"reserved_quantity\" <= \"on_hand_quantity\"");
            });

        inventoryItem.HasKey(entity => entity.Id);

        inventoryItem.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        inventoryItem.Property(entity => entity.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        inventoryItem.HasIndex(entity => entity.ProductId)
            .IsUnique();

        inventoryItem.Property(entity => entity.Sku)
            .HasColumnName("sku")
            .HasMaxLength(64)
            .IsRequired();

        inventoryItem.HasIndex(entity => entity.Sku)
            .IsUnique();

        inventoryItem.Property(entity => entity.OnHandQuantity)
            .HasColumnName("on_hand_quantity")
            .IsRequired();

        inventoryItem.Property(entity => entity.ReservedQuantity)
            .HasColumnName("reserved_quantity")
            .IsRequired();

        inventoryItem.Property(entity => entity.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        inventoryItem.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        inventoryItem.Property(entity => entity.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        inventoryItem.Ignore(entity => entity.AvailableQuantity);
    }
}
