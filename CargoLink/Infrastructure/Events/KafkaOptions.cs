namespace CargoLink.Infrastructure.Events;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = string.Empty;

    public string ClientId { get; init; } = "cargolink-api";

    public string ConsumerGroupId { get; init; } = "cargolink-dispatch";

    public KafkaTopicOptions Topics { get; init; } = new();

    public IReadOnlyList<string> GetTopicNames()
    {
        return new[]
        {
            Topics.NewBooking,
            Topics.DriverAccepted,
            Topics.Completed
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    }
}

public sealed class KafkaTopicOptions
{
    public string NewBooking { get; init; } = "dispatch.booking.created";

    public string DriverAccepted { get; init; } = "dispatch.booking.driver-accepted";

    public string Completed { get; init; } = "dispatch.booking.completed";
}
