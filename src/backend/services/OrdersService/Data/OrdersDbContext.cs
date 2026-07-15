using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrdersService.Domain;

namespace OrdersService.Data;

public sealed class OrdersDbContext(
    DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureOrder(modelBuilder.Entity<Order>());
        ConfigureOrderItem(modelBuilder.Entity<OrderItem>());
    }

    private static void ConfigureOrder(
        EntityTypeBuilder<Order> order)
    {
        order.ToTable("orders");

        order.HasKey(entity => entity.Id);

        order.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        order.Property(entity => entity.CustomerId)
            .HasColumnName("customer_id")
            .HasMaxLength(128)
            .IsRequired();

        order.HasIndex(entity => entity.CustomerId);

        order.Property(entity => entity.CustomerEmail)
            .HasColumnName("customer_email")
            .HasMaxLength(320)
            .IsRequired();

        order.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        order.HasIndex(entity => entity.Status);

        order.Property(entity => entity.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        order.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        order.Property(entity => entity.PaymentMethod)
            .HasColumnName("payment_method")
            .HasMaxLength(64)
            .IsRequired();

        order.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        order.HasIndex(entity => entity.CreatedAtUtc);

        order.Property(entity => entity.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        order.HasMany(entity => entity.Items)
            .WithOne()
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        order.Navigation(entity => entity.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureOrderItem(
        EntityTypeBuilder<OrderItem> item)
    {
        item.ToTable("order_items");

        item.HasKey(entity => entity.Id);

        item.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        item.Property(entity => entity.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        item.HasIndex(entity => entity.OrderId);

        item.Property(entity => entity.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        item.Property(entity => entity.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(200)
            .IsRequired();

        item.Property(entity => entity.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2)
            .IsRequired();

        item.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        item.Property(entity => entity.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        item.Ignore(entity => entity.LineTotal);
    }
}
