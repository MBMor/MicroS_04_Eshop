using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationsService.Domain;

namespace NotificationsService.Data;

public sealed class NotificationsDbContext(
    DbContextOptions<NotificationsDbContext> options)
    : DbContext(options)
{
    public DbSet<Notification> Notifications =>
        Set<Notification>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureNotification(
            modelBuilder.Entity<Notification>());
    }

    private static void ConfigureNotification(
        EntityTypeBuilder<Notification> notification)
    {
        notification.ToTable(
            "notifications",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_notifications_read_state",
                    """
                    (
                        "is_read" = FALSE
                        AND "read_at_utc" IS NULL
                    )
                    OR
                    (
                        "is_read" = TRUE
                        AND "read_at_utc" IS NOT NULL
                    )
                    """);
            });

        notification.HasKey(entity => entity.Id);

        notification.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        notification.Property(entity => entity.CustomerId)
            .HasColumnName("customer_id")
            .HasMaxLength(128)
            .IsRequired();

        notification.Property(entity => entity.OrderId)
            .HasColumnName("order_id");

        notification.Property(entity => entity.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        notification.Property(entity => entity.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        notification.Property(entity => entity.Message)
            .HasColumnName("message")
            .HasMaxLength(2_000)
            .IsRequired();

        notification.Property(entity => entity.IsRead)
            .HasColumnName("is_read")
            .IsRequired();

        notification.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        notification.Property(entity => entity.ReadAtUtc)
            .HasColumnName("read_at_utc");

        notification.Property(entity => entity.SourceEventId)
            .HasColumnName("source_event_id");

        notification.Property(entity => entity.CorrelationId)
            .HasColumnName("correlation_id");

        notification.HasIndex(entity => new
        {
            entity.CustomerId,
            entity.CreatedAtUtc
        })
            .IsDescending(false, true)
            .HasDatabaseName(
                "ix_notifications_customer_created");

        notification.HasIndex(entity => new
        {
            entity.CustomerId,
            entity.IsRead,
            entity.CreatedAtUtc
        })
            .IsDescending(false, false, true)
            .HasDatabaseName(
                "ix_notifications_customer_read_created");

        notification.HasIndex(entity => entity.OrderId)
            .HasDatabaseName(
                "ix_notifications_order_id");

        notification.HasIndex(entity => entity.SourceEventId)
            .HasDatabaseName(
                "ix_notifications_source_event_id");
    }
}
