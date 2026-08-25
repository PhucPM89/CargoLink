namespace CargoLink.Infrastructure.Events;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; init; } = 20;

    public int PollingIntervalMilliseconds { get; init; } = 1000;

    public int LockTimeoutSeconds { get; init; } = 30;
}
