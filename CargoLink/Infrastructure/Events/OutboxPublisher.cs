using CargoLink.Abstractions;
using CargoLink.Domain.Entities;
using CargoLink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoLink.Infrastructure.Events;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IKafkaEventPublisher kafkaEventPublisher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IKafkaEventPublisher _kafkaEventPublisher = kafkaEventPublisher;
    private readonly OutboxOptions _options = options.Value;
    private readonly ILogger<OutboxPublisher> _logger = logger;
    private readonly string _workerId = Guid.NewGuid().ToString("N");
    private bool _kafkaDisabledLogged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessBatchAsync(stoppingToken);
                if (processedCount == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(250, _options.PollingIntervalMilliseconds)), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox publisher failed.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!_kafkaEventPublisher.IsEnabled)
        {
            if (!_kafkaDisabledLogged)
            {
                _logger.LogWarning("Outbox publisher is idle because Kafka is disabled. Pending outbox messages will remain in the database.");
                _kafkaDisabledLogged = true;
            }

            return 0;
        }

        _kafkaDisabledLogged = false;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var messages = await ClaimBatchAsync(dbContext, cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            await PublishSingleAsync(dbContext, message, cancellationToken);
        }

        return messages.Count;
    }

    private async Task<List<OutboxMessage>> ClaimBatchAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            var now = DateTimeOffset.UtcNow;
            var leaseUntil = now.AddSeconds(Math.Max(10, _options.LockTimeoutSeconds));
            var batchSize = Math.Max(1, _options.BatchSize);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var messages = await dbContext.OutboxMessages
                .FromSqlRaw(
                    "SELECT * " +
                    "FROM outbox_messages " +
                    "WHERE ProcessedAt IS NULL " +
                    "AND (LockedUntil IS NULL OR LockedUntil < {0}) " +
                    $"ORDER BY OccurredAt LIMIT {batchSize} FOR UPDATE SKIP LOCKED",
                    now)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return messages;
            }

            foreach (var message in messages)
            {
                message.LockId = _workerId;
                message.LockedUntil = leaseUntil;
                message.LastAttemptAt = now;
                message.AttemptCount += 1;
                message.LastError = null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return messages;
        });
    }

    private async Task PublishSingleAsync(
        ApplicationDbContext dbContext,
        OutboxMessage claimedMessage,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxMessages.FirstOrDefaultAsync(
            x => x.Id == claimedMessage.Id && x.LockId == _workerId,
            cancellationToken);

        if (message is null)
        {
            return;
        }

        try
        {
            await _kafkaEventPublisher.PublishAsync(message, cancellationToken);
            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.LockedUntil = null;
            message.LockId = null;
            message.LastError = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            message.LockedUntil = null;
            message.LockId = null;
            message.LastError = Truncate(exception.Message, 2000);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogError(exception, "Failed to publish outbox message {OutboxMessageId} to Kafka topic {Topic}", message.Id, message.Topic);
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
