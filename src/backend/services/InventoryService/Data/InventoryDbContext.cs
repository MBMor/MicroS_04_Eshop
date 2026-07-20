using InventoryService.Domain;
using InventoryService.Inbox;
using InventoryService.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Data;

public sealed class InventoryDbContext(
    DbContextOptions<InventoryDbContext> options)
    : DbContext(options)
{
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureInventoryItem(modelBuilder.Entity<InventoryItem>());
        ConfigureOutboxMessage(modelBuilder.Entity<OutboxMessage>());
        ConfigureProcessedMessage(modelBuilder.Entity<ProcessedMessage>());
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

    private static void ConfigureOutboxMessage(EntityTypeBuilder<OutboxMessage> message)
    {
        message.ToTable("outbox_messages");

        message.HasKey(entity => entity.Id);

        message.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        message.Property(entity => entity.EventId).HasColumnName("event_id").IsRequired();
        message.Property(entity => entity.EventType).HasColumnName("event_type").HasMaxLength(256).IsRequired();
        message.Property(entity => entity.RoutingKey).HasColumnName("routing_key").HasMaxLength(256).IsRequired();
        message.Property(entity => entity.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        message.Property(entity => entity.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        message.Property(entity => entity.CorrelationId).HasColumnName("correlation_id").IsRequired();
        message.Property(entity => entity.TraceParent).HasColumnName("trace_parent").HasMaxLength(512);
        message.Property(entity => entity.TraceState).HasColumnName("trace_state").HasMaxLength(512);
        message.Property(entity => entity.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        message.Property(entity => entity.RetryCount).HasColumnName("retry_count").IsRequired();
        message.Property(entity => entity.LastError).HasColumnName("last_error").HasMaxLength(4_000);
        message.Property(entity => entity.PublishedAtUtc).HasColumnName("published_at_utc");

        message.Property(entity => entity.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");

        message.Property(entity => entity.ClaimedAtUtc).HasColumnName("claimed_at_utc");

        message.Property(entity => entity.ClaimedBy).HasColumnName("claimed_by").HasMaxLength(200);

        message.HasIndex(entity => entity.EventId).IsUnique();

        message.HasIndex(entity => new{entity.Status, entity.OccurredAtUtc});

        message.HasIndex(entity => new{entity.Status, entity.ClaimedAtUtc});

        message.HasIndex(entity => new { entity.Status, entity.NextAttemptAtUtc });

        message.HasIndex(entity => new{entity.Status, entity.PublishedAtUtc});
    }

    private static void ConfigureProcessedMessage(
    EntityTypeBuilder<ProcessedMessage> processedMessage)
    {
        processedMessage.ToTable("processed_messages");

        processedMessage.HasKey(entity => new
        {
            entity.EventId,
            entity.ConsumerName
        });

        processedMessage.Property(entity => entity.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        processedMessage.Property(entity => entity.ConsumerName)
            .HasColumnName("consumer_name")
            .HasMaxLength(256)
            .IsRequired();

        processedMessage.Property(entity => entity.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .IsRequired();

        processedMessage.HasIndex(entity => entity.ProcessedAtUtc);
    }
}
