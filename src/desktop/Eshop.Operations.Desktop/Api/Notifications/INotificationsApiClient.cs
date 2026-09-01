namespace Eshop.Operations.Desktop.Api.Notifications;

public interface INotificationsApiClient
{
    Task<OperationalNotificationPageDto>
        GetNotificationsAsync(
            Guid? orderId,
            string? customerId,
            Guid? correlationId,
            int offset,
            int limit,
            CancellationToken cancellationToken);

    Task<OperationalNotificationDto>
        GetNotificationAsync(
            Guid notificationId,
            CancellationToken cancellationToken);
}