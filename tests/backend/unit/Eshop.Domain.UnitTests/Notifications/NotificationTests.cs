using NotificationsService.Domain;
using Xunit;

namespace Eshop.Domain.UnitTests.Notifications;

public sealed class NotificationTests
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
    public void Create_ValidData_CreatesUnreadNotification()
    {
        Guid notificationId = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();

        Notification notification = Notification.Create(
            notificationId,
            "  customer-1  ",
            orderId,
            NotificationType.OrderCreated,
            "  Order created  ",
            "  Your order was created.  ",
            CreatedAtUtc,
            eventId,
            correlationId);

        Assert.Equal(notificationId, notification.Id);
        Assert.Equal("customer-1", notification.CustomerId);
        Assert.Equal(orderId, notification.OrderId);

        Assert.Equal(
            NotificationType.OrderCreated,
            notification.Type);

        Assert.Equal("Order created", notification.Title);

        Assert.Equal(
            "Your order was created.",
            notification.Message);

        Assert.False(notification.IsRead);
        Assert.Equal(CreatedAtUtc, notification.CreatedAtUtc);
        Assert.Null(notification.ReadAtUtc);
        Assert.Equal(eventId, notification.SourceEventId);
        Assert.Equal(correlationId, notification.CorrelationId);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Notification.Create(
                Guid.Empty,
                "customer-1",
                orderId: null,
                NotificationType.OrderCreated,
                "Order created",
                "Order created.",
                CreatedAtUtc));
    }

    [Fact]
    public void Create_UnsupportedType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Notification.Create(
                Guid.NewGuid(),
                "customer-1",
                orderId: null,
                (NotificationType)999,
                "Title",
                "Message",
                CreatedAtUtc));
    }

    [Fact]
    public void Create_EmptyOptionalOrderId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Notification.Create(
                Guid.NewGuid(),
                "customer-1",
                Guid.Empty,
                NotificationType.OrderCreated,
                "Order created",
                "Order created.",
                CreatedAtUtc));
    }

    [Fact]
    public void Create_BlankTitle_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Notification.Create(
                Guid.NewGuid(),
                "customer-1",
                orderId: null,
                NotificationType.OrderCreated,
                "   ",
                "Order created.",
                CreatedAtUtc));
    }

    [Fact]
    public void Create_TitleAboveMaximumLength_Throws()
    {
        string title = new(
            'a',
            count: 201);

        Assert.Throws<ArgumentException>(
            () => Notification.Create(
                Guid.NewGuid(),
                "customer-1",
                orderId: null,
                NotificationType.OrderCreated,
                title,
                "Order created.",
                CreatedAtUtc));
    }

    [Fact]
    public void Create_MessageAboveMaximumLength_Throws()
    {
        string message = new(
            'a',
            count: 2_001);

        Assert.Throws<ArgumentException>(
            () => Notification.Create(
                Guid.NewGuid(),
                "customer-1",
                orderId: null,
                NotificationType.OrderCreated,
                "Order created",
                message,
                CreatedAtUtc));
    }

    [Fact]
    public void MarkAsRead_UnreadNotification_MarksNotificationRead()
    {
        Notification notification =
            CreateNotification();

        DateTimeOffset readAtUtc =
            CreatedAtUtc.AddMinutes(1);

        bool changed =
            notification.MarkAsRead(readAtUtc);

        Assert.True(changed);
        Assert.True(notification.IsRead);
        Assert.Equal(readAtUtc, notification.ReadAtUtc);
    }

    [Fact]
    public void MarkAsRead_AlreadyReadNotification_IsIdempotent()
    {
        Notification notification =
            CreateNotification();

        DateTimeOffset firstReadAtUtc =
            CreatedAtUtc.AddMinutes(1);

        DateTimeOffset secondReadAtUtc =
            CreatedAtUtc.AddMinutes(2);

        Assert.True(
            notification.MarkAsRead(firstReadAtUtc));

        bool changed =
            notification.MarkAsRead(secondReadAtUtc);

        Assert.False(changed);
        Assert.True(notification.IsRead);

        Assert.Equal(
            firstReadAtUtc,
            notification.ReadAtUtc);
    }

    private static Notification CreateNotification()
    {
        return Notification.Create(
            Guid.NewGuid(),
            "customer-1",
            Guid.NewGuid(),
            NotificationType.OrderCreated,
            "Order created",
            "Your order was created.",
            CreatedAtUtc);
    }
}
