using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrdersService.Domain;
using OrdersService.Inbox;
using OrdersService.Outbox;

namespace OrdersService.Data;

public sealed class OrdersDbContext(
    DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureOrder(modelBuilder.Entity<Order>());
        ConfigureOrderItem(modelBuilder.Entity<OrderItem>());
        ConfigureOrderStatusHistory(modelBuilder.Entity<OrderStatusHistory>());
        ConfigureOutboxMessage(modelBuilder.Entity<OutboxMessage>());
        ConfigureProcessedMessage(modelBuilder.Entity<ProcessedMessage>());
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

        order.HasMany(entity => entity.StatusHistory)
            .WithOne()
            .HasForeignKey(history => history.OrderId)
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

    private static void ConfigureOrderStatusHistory(
    EntityTypeBuilder<OrderStatusHistory> history)
    {
        history.ToTable("order_status_history");

        history.HasKey(entity => entity.Id);

        history.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        history.Property(entity => entity.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        history.Property(entity => entity.FromStatus)
            .HasColumnName("from_status")
            .HasConversion<string>()
            .HasMaxLength(64);

        history.Property(entity => entity.ToStatus)
            .HasColumnName("to_status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        history.Property(entity => entity.Reason)
            .HasColumnName("reason")
            .HasMaxLength(
                OrderStatusHistory.MaximumReasonLength)
            .IsRequired();

        history.Property(entity => entity.ChangedAtUtc)
            .HasColumnName("changed_at_utc")
            .IsRequired();

        history.HasIndex(entity => new
        {
            entity.OrderId,
            entity.ChangedAtUtc
        });
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

        message.HasIndex(entity => new { entity.Status, entity.OccurredAtUtc });

        message.HasIndex(entity => new { entity.Status, entity.ClaimedAtUtc });

        message.HasIndex(entity => new { entity.Status, entity.NextAttemptAtUtc });

        message.HasIndex(entity => new { entity.Status, entity.PublishedAtUtc });
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
