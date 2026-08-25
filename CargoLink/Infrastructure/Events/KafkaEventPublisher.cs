using System.Text;
using CargoLink.Abstractions;
using CargoLink.Domain.Entities;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CargoLink.Infrastructure.Events;

public sealed class KafkaEventPublisher : IKafkaEventPublisher, IDisposable
{
    private readonly IProducer<string, string>? _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        var kafkaOptions = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(kafkaOptions.BootstrapServers))
        {
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            ClientId = kafkaOptions.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public bool IsEnabled => _producer is not null;

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        if (_producer is null || string.IsNullOrWhiteSpace(message.Topic))
        {
            _logger.LogDebug("Kafka publish skipped for outbox message {OutboxMessageId}", message.Id);
            return;
        }

        await _producer.ProduceAsync(
            message.Topic,
            new Message<string, string>
            {
                Key = message.MessageKey,
                Value = message.Payload,
                Headers = new Headers
                {
                    { "message-id", Encoding.UTF8.GetBytes(message.Id.ToString()) },
                    { "message-type", Encoding.UTF8.GetBytes(message.MessageType) },
                    { "occurred-at", Encoding.UTF8.GetBytes(message.OccurredAt.ToString("O")) }
                }
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(3));
        _producer?.Dispose();
    }
}
