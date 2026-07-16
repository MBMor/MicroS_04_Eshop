using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentsService.Domain;

namespace PaymentsService.Data;

public sealed class PaymentsDbContext(
    DbContextOptions<PaymentsDbContext> options)
    : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePayment(modelBuilder.Entity<Payment>());
    }

    private static void ConfigurePayment(
        EntityTypeBuilder<Payment> payment)
    {
        payment.ToTable(
            "payments",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_payments_amount_positive",
                    "\"amount\" > 0");

                tableBuilder.HasCheckConstraint(
                    "ck_payments_processed_status",
                    """
                    (
                        "status" = 'Pending'
                        AND "processed_at_utc" IS NULL
                    )
                    OR
                    (
                        "status" IN ('Authorized', 'Failed')
                        AND "processed_at_utc" IS NOT NULL
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    "ck_payments_failure_reason",
                    """
                    (
                        "status" = 'Failed'
                        AND "failure_reason" IS NOT NULL
                    )
                    OR
                    (
                        "status" <> 'Failed'
                        AND "failure_reason" IS NULL
                    )
                    """);
            });

        payment.HasKey(entity => entity.Id);

        payment.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        payment.Property(entity => entity.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        payment.HasIndex(entity => entity.OrderId)
            .IsUnique();

        payment.Property(entity => entity.CustomerId)
            .HasColumnName("customer_id")
            .HasMaxLength(128)
            .IsRequired();

        payment.HasIndex(entity => entity.CustomerId);

        payment.Property(entity => entity.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        payment.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        payment.Property(entity => entity.PaymentMethod)
            .HasColumnName("payment_method")
            .HasMaxLength(64)
            .IsRequired();

        payment.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        payment.HasIndex(entity => entity.Status);

        payment.Property(entity => entity.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(500);

        payment.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        payment.HasIndex(entity => entity.CreatedAtUtc);

        payment.Property(entity => entity.ProcessedAtUtc)
            .HasColumnName("processed_at_utc");
    }
}
