namespace OrdersService.Outbox;

public enum OutboxMessageStatus
{
    Pending = 1,
    Published = 2,
    Failed = 3
}
