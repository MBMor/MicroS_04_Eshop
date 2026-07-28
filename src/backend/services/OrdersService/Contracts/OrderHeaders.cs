namespace OrdersService.Contracts;

public static class OrderHeaders
{
    public const string IdempotencyKey =
        "Idempotency-Key";

    public const string IdempotentReplayed =
        "Idempotent-Replayed";
}
