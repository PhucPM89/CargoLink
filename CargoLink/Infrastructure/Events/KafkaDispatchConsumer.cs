using System.Text;
using System.Text.Json;
using CargoLink.Domain.Entities;
using CargoLink.Contracts.Bookings;
using CargoLink.Domain.Events;
using CargoLink.Hubs;
using CargoLink.Infrastructure.Data;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoLink.Infrastructure.Events;

public sealed class KafkaDispatchConsumer(
    IOptions<KafkaOptions> options,
    IHubContext<DispatchHub> hubContext,
    IServiceScopeFactory scopeFactory,
    ILogger<KafkaDispatchConsumer> logger) : BackgroundService
{
    private const string ConsumerName = nameof(KafkaDispatchConsumer);
    private static readonly TimeSpan InboxLockDuration = TimeSpan.FromSeconds(60);
    private readonly KafkaOptions _options = options.Value;
    private readonly IHubContext<DispatchHub> _hubContext = hubContext;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<KafkaDispatchConsumer> _logger = logger;
    private readonly string _workerId = Guid.NewGuid().ToString("N");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        if (string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            _logger.LogWarning("Kafka consumer is disabled because Kafka:BootstrapServers is empty.");
            return;
        }

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            ClientId = $"{_options.ClientId}-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        consumer.Subscribe(new[]
        {
            _options.Topics.NewBooking,
            _options.Topics.DriverAccepted,
            _options.Topics.Completed
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                {
                    continue;
                }

                var inboxEnvelope = CreateInboxEnvelope(result);
                var claimState = await TryClaimInboxMessageAsync(inboxEnvelope, stoppingToken);
                if (claimState == InboxClaimState.AlreadyProcessed)
                {
                    consumer.Commit(result);
                    continue;
                }

                if (claimState == InboxClaimState.Locked)
                {
                    _logger.LogDebug(
                        "Kafka message {MessageId} is already being processed by another consumer instance.",
                        inboxEnvelope.MessageId);
                    await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
                    continue;
                }

                try
                {
                    await DispatchAsync(result.Topic, result.Message.Value, stoppingToken);
                    await MarkInboxMessageProcessedAsync(inboxEnvelope, stoppingToken);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    await ReleaseInboxMessageAsync(inboxEnvelope, exception, stoppingToken);
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException exception)
            {
                _logger.LogError(exception, "Kafka consume failure");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected Kafka consumer failure");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private Task DispatchAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (topic == _options.Topics.NewBooking)
        {
            var message = JsonSerializer.Deserialize<NewBookingCreatedEvent>(payload, JsonDefaults.Default);
            if (message is null)
            {
                return Task.CompletedTask;
            }

            return _hubContext.Clients.Group(DispatchHub.DispatchersGroup)
                .SendAsync("bookingCreated", message, cancellationToken);
        }

        if (topic == _options.Topics.DriverAccepted)
        {
            var message = JsonSerializer.Deserialize<DriverTripAcceptedEvent>(payload, JsonDefaults.Default);
            if (message is null)
            {
                return Task.CompletedTask;
            }

            var update = new BookingRealtimeUpdate
            {
                BookingId = message.BookingId,
                BookingNumber = message.BookingNumber,
                Status = "DriverAssigned",
                DriverId = message.DriverId,
                DriverName = message.DriverName,
                VehiclePlateNumber = message.VehiclePlateNumber,
                Timestamp = message.AcceptedAt
            };

            return BroadcastBookingStatusAsync(update, cancellationToken);
        }

        if (topic == _options.Topics.Completed)
        {
            var message = JsonSerializer.Deserialize<BookingCompletedEvent>(payload, JsonDefaults.Default);
            if (message is null)
            {
                return Task.CompletedTask;
            }

            var update = new BookingRealtimeUpdate
            {
                BookingId = message.BookingId,
                BookingNumber = message.BookingNumber,
                Status = "Completed",
                DriverId = message.DriverId,
                Timestamp = message.CompletedAt
            };

            return BroadcastBookingStatusAsync(update, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private async Task BroadcastBookingStatusAsync(BookingRealtimeUpdate update, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.Group(DispatchHub.DispatchersGroup)
            .SendAsync("bookingStatusUpdated", update, cancellationToken);
        await _hubContext.Clients.Group(DispatchHub.GetBookingGroup(update.BookingId))
            .SendAsync("bookingStatusUpdated", update, cancellationToken);
    }

    private InboxEnvelope CreateInboxEnvelope(ConsumeResult<Ignore, string> result)
    {
        return new InboxEnvelope(
            GetMessageId(result),
            result.Topic,
            result.Message.Value);
    }

    private async Task<InboxClaimState> TryClaimInboxMessageAsync(InboxEnvelope envelope, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var strategy = dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();

                var now = DateTimeOffset.UtcNow;
                var lockUntil = now.Add(InboxLockDuration);

                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                var inboxMessage = await dbContext.InboxMessages
                    .FirstOrDefaultAsync(
                        x => x.MessageId == envelope.MessageId && x.Consumer == ConsumerName,
                        cancellationToken);

                if (inboxMessage is not null)
                {
                    if (inboxMessage.ProcessedAt.HasValue)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return InboxClaimState.AlreadyProcessed;
                    }

                    if (inboxMessage.LockedUntil.HasValue
                        && inboxMessage.LockedUntil.Value >= now
                        && inboxMessage.LockId != _workerId)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return InboxClaimState.Locked;
                    }
                }

                inboxMessage ??= new InboxMessage
                {
                    MessageId = envelope.MessageId,
                    Consumer = ConsumerName,
                    ReceivedAt = now
                };

                if (dbContext.Entry(inboxMessage).State == EntityState.Detached)
                {
                    dbContext.InboxMessages.Add(inboxMessage);
                }

                inboxMessage.Topic = envelope.Topic;
                inboxMessage.Payload = envelope.Payload;
                inboxMessage.LastAttemptAt = now;
                inboxMessage.AttemptCount += 1;
                inboxMessage.LockId = _workerId;
                inboxMessage.LockedUntil = lockUntil;
                inboxMessage.LastError = null;

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return InboxClaimState.Claimed;
            });
        }
        catch (DbUpdateException exception) when (IsDuplicateInboxClaim(exception))
        {
            _logger.LogDebug(
                exception,
                "Kafka message {MessageId} was already claimed by another consumer instance.",
                envelope.MessageId);
            return InboxClaimState.Locked;
        }
    }

    private async Task MarkInboxMessageProcessedAsync(InboxEnvelope envelope, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var inboxMessage = await dbContext.InboxMessages
            .FirstOrDefaultAsync(
                x => x.MessageId == envelope.MessageId && x.Consumer == ConsumerName && x.LockId == _workerId,
                cancellationToken);

        if (inboxMessage is null)
        {
            return;
        }

        inboxMessage.ProcessedAt = DateTimeOffset.UtcNow;
        inboxMessage.LockId = null;
        inboxMessage.LockedUntil = null;
        inboxMessage.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReleaseInboxMessageAsync(
        InboxEnvelope envelope,
        Exception exception,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var inboxMessage = await dbContext.InboxMessages
            .FirstOrDefaultAsync(
                x => x.MessageId == envelope.MessageId && x.Consumer == ConsumerName && x.LockId == _workerId,
                cancellationToken);

        if (inboxMessage is null)
        {
            return;
        }

        inboxMessage.LockId = null;
        inboxMessage.LockedUntil = null;
        inboxMessage.LastError = Truncate(exception.Message, 2000);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetMessageId(ConsumeResult<Ignore, string> result)
    {
        if (result.Message.Headers.TryGetLastBytes("message-id", out var headerBytes)
            && headerBytes is { Length: > 0 })
        {
            return Encoding.UTF8.GetString(headerBytes);
        }

        return $"{result.Topic}:{result.Partition.Value}:{result.Offset.Value}";
    }

    private static bool IsDuplicateInboxClaim(DbUpdateException exception)
    {
        return exception.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record InboxEnvelope(string MessageId, string Topic, string Payload);

    private enum InboxClaimState
    {
        Claimed,
        AlreadyProcessed,
        Locked
    }
}
