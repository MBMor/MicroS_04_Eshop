using System.Text.Json;
using System.Text.Json.Serialization;

namespace Messaging.Shared.Serialization;

public sealed class SystemTextJsonMessageSerializer
    : IMessageSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

    public byte[] Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return JsonSerializer.SerializeToUtf8Bytes(
            value,
            SerializerOptions);
    }

    public T Deserialize<T>(ReadOnlySpan<byte> body)
    {
        T? result = JsonSerializer.Deserialize<T>(
            body,
            SerializerOptions);

        return result
            ?? throw new JsonException(
                $"Message body could not be deserialized as '{typeof(T).FullName}'.");
    }
}
