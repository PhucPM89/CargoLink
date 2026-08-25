using System.Text.Json;
using CargoLink.Domain.Entities;

namespace CargoLink.Infrastructure.Events;

public static class OutboxMessageFactory
{
    public static OutboxMessage Create<T>(string topic, string key, T message)
    {
        return new OutboxMessage
        {
            Topic = topic,
            MessageKey = key,
            MessageType = typeof(T).FullName ?? typeof(T).Name,
            Payload = JsonSerializer.Serialize(message, JsonDefaults.Default),
            OccurredAt = DateTimeOffset.UtcNow
        };
    }
}
