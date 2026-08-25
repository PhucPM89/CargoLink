using CargoLink.Domain.Entities;

namespace CargoLink.Abstractions;

public interface IKafkaEventPublisher
{
    bool IsEnabled { get; }

    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
