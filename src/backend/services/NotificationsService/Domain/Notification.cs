namespace NotificationsService.Domain;

public sealed class Notification
{
    private const int MaxCustomerIdLength = 128;
    private const int MaxTitleLength = 200;
    private const int MaxMessageLength = 2_000;

    private Notification()
    {
    }

    public Guid Id { get; private set; }

    public string CustomerId { get; private set; } = string.Empty;

    public Guid? OrderId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public Guid? SourceEventId { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public static Notification Create(
        Guid id,
        string customerId,
        Guid? orderId,
        NotificationType type,
        string title,
        string message,
        DateTimeOffset createdAtUtc,
        Guid? sourceEventId = null,
        Guid? correlationId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Notification id must not be empty.",
                nameof(id));
        }

        ValidateOptionalGuid(orderId, nameof(orderId));
        ValidateOptionalGuid(sourceEventId, nameof(sourceEventId));
        ValidateOptionalGuid(correlationId, nameof(correlationId));

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Notification type is not supported.");
        }

        return new Notification
        {
            Id = id,
            CustomerId = RequiredTrimmed(
                customerId,
                nameof(customerId),
                MaxCustomerIdLength),
            OrderId = orderId,
            Type = type,
            Title = RequiredTrimmed(
                title,
                nameof(title),
                MaxTitleLength),
            Message = RequiredTrimmed(
                message,
                nameof(message),
                MaxMessageLength),
            IsRead = false,
            CreatedAtUtc = createdAtUtc,
            ReadAtUtc = null,
            SourceEventId = sourceEventId,
            CorrelationId = correlationId
        };
    }

    public bool MarkAsRead(DateTimeOffset readAtUtc)
    {
        if (IsRead)
        {
            return false;
        }

        IsRead = true;
        ReadAtUtc = readAtUtc;

        return true;
    }

    private static string RequiredTrimmed(
        string value,
        string parameterName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value must not be empty.",
                parameterName);
        }

        string normalizedValue = value.Trim();

        if (normalizedValue.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value must not exceed {maxLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }

    private static void ValidateOptionalGuid(
        Guid? value,
        string parameterName)
    {
        if (value.HasValue && value.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Optional identifier must not be an empty GUID.",
                parameterName);
        }
    }
}
