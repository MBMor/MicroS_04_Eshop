namespace Eshop.Observability.Correlation;

public static class CorrelationIdGenerator
{
    public static Guid Create()
    {
        return Guid.NewGuid();
    }
}
