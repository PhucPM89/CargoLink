using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;

namespace CargoLink.Infrastructure.Events;

public sealed class KafkaTopicInitializer(
    IOptions<KafkaOptions> options,
    ILogger<KafkaTopicInitializer> logger)
{
    private readonly KafkaOptions _options = options.Value;
    private readonly ILogger<KafkaTopicInitializer> _logger = logger;

    public async Task EnsureTopicsExistAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            _logger.LogWarning("Kafka topic initialization skipped because Kafka:BootstrapServers is empty.");
            return;
        }

        var topicNames = _options.GetTopicNames();
        if (topicNames.Count == 0)
        {
            return;
        }

        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = $"{_options.ClientId}-admin"
        }).Build();

        try
        {
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
            var existingTopics = metadata.Topics
                .Select(x => x.Topic)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.Ordinal);

            var missingTopics = topicNames
                .Where(x => !existingTopics.Contains(x))
                .Select(x => new TopicSpecification
                {
                    Name = x,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                })
                .ToList();

            if (missingTopics.Count == 0)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            await adminClient.CreateTopicsAsync(missingTopics);
            _logger.LogInformation(
                "Kafka topics created: {TopicNames}",
                string.Join(", ", missingTopics.Select(x => x.Name)));
        }
        catch (CreateTopicsException exception)
        {
            var nonExistingErrors = exception.Results
                .Where(x => x.Error.Code != ErrorCode.TopicAlreadyExists)
                .ToList();

            if (nonExistingErrors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Kafka topic initialization failed: {string.Join(", ", nonExistingErrors.Select(x => $"{x.Topic}:{x.Error.Reason}"))}",
                exception);
        }
    }
}
