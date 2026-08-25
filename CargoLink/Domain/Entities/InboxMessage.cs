namespace CargoLink.Domain.Entities;

public class InboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string MessageId { get; set; } = string.Empty;

    public string Consumer { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public int AttemptCount { get; set; }

    public string? LockId { get; set; }

    public string? LastError { get; set; }
}
