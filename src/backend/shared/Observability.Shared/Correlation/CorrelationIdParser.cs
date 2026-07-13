namespace Observability.Shared.Correlation;

public static class CorrelationIdParser
{
    public static bool TryParse(string? value, out Guid correlationId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            correlationId = default;
            return false;
        }

        return Guid.TryParse(value, out correlationId);
    }

    public static Guid ParseOrCreate(string? value)
    {
        return TryParse(value, out Guid correlationId)
            ? correlationId
            : CorrelationIdGenerator.Create();
    }
}
