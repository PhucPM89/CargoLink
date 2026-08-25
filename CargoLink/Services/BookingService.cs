using CargoLink.Contracts.Bookings;
using CargoLink.Domain.Entities;
using CargoLink.Domain.Enums;
using CargoLink.Domain.Events;
using CargoLink.Infrastructure.Data;
using CargoLink.Infrastructure.Events;
using CargoLink.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoLink.Services;

public sealed class BookingService(
    ApplicationDbContext dbContext,
    IOptions<KafkaOptions> kafkaOptions,
    RedisDriverGeoService redisDriverGeoService)
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly KafkaOptions _kafkaOptions = kafkaOptions.Value;
    private readonly RedisDriverGeoService _redisDriverGeoService = redisDriverGeoService;

    public async Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(token =>
        {
            var now = DateTimeOffset.UtcNow;
            var booking = new Booking
            {
                BookingNumber = GenerateBookingNumber(),
                CustomerName = request.CustomerName.Trim(),
                PickupAddress = request.PickupAddress.Trim(),
                DropoffAddress = request.DropoffAddress.Trim(),
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                DropoffLatitude = request.DropoffLatitude,
                DropoffLongitude = request.DropoffLongitude,
                EstimatedWeightTons = request.EstimatedWeightTons,
                Status = BookingStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                StatusHistory =
                {
                    new BookingStatusHistory
                    {
                        Status = BookingStatus.Pending,
                        Note = "Booking created",
                        CreatedAt = now
                    }
                }
            };

            var bookingCreated = new NewBookingCreatedEvent(
                booking.Id,
                booking.BookingNumber,
                booking.CustomerName,
                booking.PickupAddress,
                booking.DropoffAddress,
                booking.CreatedAt);

            _dbContext.Bookings.Add(booking);
            _dbContext.OutboxMessages.Add(OutboxMessageFactory.Create(_kafkaOptions.Topics.NewBooking, booking.Id.ToString(), bookingCreated));
            return Task.FromResult(ToResponse(booking));
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<BookingResponse>> GetActiveBookingsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.Vehicle)
            .Where(x => x.Status != BookingStatus.Completed && x.Status != BookingStatus.Cancelled)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BookingResponse
            {
                Id = x.Id,
                BookingNumber = x.BookingNumber,
                CustomerName = x.CustomerName,
                PickupAddress = x.PickupAddress,
                DropoffAddress = x.DropoffAddress,
                PickupLatitude = x.PickupLatitude,
                PickupLongitude = x.PickupLongitude,
                DropoffLatitude = x.DropoffLatitude,
                DropoffLongitude = x.DropoffLongitude,
                EstimatedWeightTons = x.EstimatedWeightTons,
                Status = x.Status.ToString(),
                DriverId = x.DriverId,
                DriverName = x.Driver != null ? x.Driver.FullName : null,
                VehicleId = x.VehicleId,
                VehiclePlateNumber = x.Vehicle != null ? x.Vehicle.PlateNumber : null,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<BookingResponse> AssignDriverAsync(Guid bookingId, Guid driverId, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteInTransactionAsync(async token =>
        {
            var booking = await _dbContext.Bookings
                .Include(x => x.Driver)
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.Id == bookingId, token)
                ?? throw new KeyNotFoundException("Booking not found.");

            if (booking.Status != BookingStatus.Pending)
            {
                throw new InvalidOperationException("Only pending bookings can be assigned.");
            }

            var driver = await _dbContext.Drivers
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.Id == driverId, token)
                ?? throw new KeyNotFoundException("Driver not found.");

            if (driver.Status != DriverStatus.Available)
            {
                throw new InvalidOperationException("Driver is not available.");
            }

            if (driver.Vehicle is null)
            {
                throw new InvalidOperationException("Driver does not have a vehicle.");
            }

            var now = DateTimeOffset.UtcNow;
            booking.DriverId = driver.Id;
            booking.Driver = driver;
            booking.VehicleId = driver.Vehicle.Id;
            booking.Vehicle = driver.Vehicle;
            booking.Status = BookingStatus.DriverAssigned;
            booking.UpdatedAt = now;
            driver.Status = DriverStatus.Busy;

            _dbContext.BookingStatusHistory.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                Status = BookingStatus.DriverAssigned,
                Note = $"Driver {driver.FullName} accepted the trip.",
                CreatedAt = now
            });

            var acceptedEvent = new DriverTripAcceptedEvent(
                booking.Id,
                booking.BookingNumber,
                driver.Id,
                driver.FullName,
                driver.Vehicle.PlateNumber,
                now);

            _dbContext.OutboxMessages.Add(OutboxMessageFactory.Create(_kafkaOptions.Topics.DriverAccepted, booking.Id.ToString(), acceptedEvent));
            return new BookingCommandResult(
                ToResponse(booking),
                new DriverGeoSyncRequest(driver.Id, driver.Status, driver.CurrentLatitude, driver.CurrentLongitude, driver.LastLocationUpdatedAt.HasValue));
        }, cancellationToken);

        await SyncDriverGeoAsync(result.DriverGeoSyncRequest, cancellationToken);
        return result.Response;
    }

    public async Task<BookingResponse> CompleteBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteInTransactionAsync(async token =>
        {
            var booking = await _dbContext.Bookings
                .Include(x => x.Driver)
                .ThenInclude(x => x!.Vehicle)
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.Id == bookingId, token)
                ?? throw new KeyNotFoundException("Booking not found.");

            if (booking.Status == BookingStatus.Completed)
            {
                throw new InvalidOperationException("Booking is already completed.");
            }

            if (booking.Driver is null)
            {
                throw new InvalidOperationException("Booking has no assigned driver.");
            }

            var now = DateTimeOffset.UtcNow;
            booking.Status = BookingStatus.Completed;
            booking.CompletedAt = now;
            booking.UpdatedAt = now;
            booking.Driver.Status = DriverStatus.Available;

            _dbContext.BookingStatusHistory.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                Status = BookingStatus.Completed,
                Note = "Booking completed successfully.",
                CreatedAt = now
            });

            var completedEvent = new BookingCompletedEvent(
                booking.Id,
                booking.BookingNumber,
                booking.Driver.Id,
                now);

            _dbContext.OutboxMessages.Add(OutboxMessageFactory.Create(_kafkaOptions.Topics.Completed, booking.Id.ToString(), completedEvent));
            return new BookingCommandResult(
                ToResponse(booking),
                new DriverGeoSyncRequest(
                    booking.Driver.Id,
                    booking.Driver.Status,
                    booking.Driver.CurrentLatitude,
                    booking.Driver.CurrentLongitude,
                    booking.Driver.LastLocationUpdatedAt.HasValue));
        }, cancellationToken);

        await SyncDriverGeoAsync(result.DriverGeoSyncRequest, cancellationToken);
        return result.Response;
    }

    private async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var result = await operation(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static BookingResponse ToResponse(Booking booking)
    {
        return new BookingResponse
        {
            Id = booking.Id,
            BookingNumber = booking.BookingNumber,
            CustomerName = booking.CustomerName,
            PickupAddress = booking.PickupAddress,
            DropoffAddress = booking.DropoffAddress,
            PickupLatitude = booking.PickupLatitude,
            PickupLongitude = booking.PickupLongitude,
            DropoffLatitude = booking.DropoffLatitude,
            DropoffLongitude = booking.DropoffLongitude,
            EstimatedWeightTons = booking.EstimatedWeightTons,
            Status = booking.Status.ToString(),
            DriverId = booking.DriverId,
            DriverName = booking.Driver?.FullName,
            VehicleId = booking.VehicleId,
            VehiclePlateNumber = booking.Vehicle?.PlateNumber,
            CreatedAt = booking.CreatedAt,
            UpdatedAt = booking.UpdatedAt,
            CompletedAt = booking.CompletedAt
        };
    }

    private async Task SyncDriverGeoAsync(DriverGeoSyncRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return;
        }

        if (request.Status == DriverStatus.Available && !request.HasKnownLocation)
        {
            return;
        }

        await _redisDriverGeoService.UpdateDriverLocationAsync(
            request.DriverId,
            request.Latitude,
            request.Longitude,
            request.Status,
            cancellationToken);
    }

    private static string GenerateBookingNumber()
    {
        return $"BKG-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
    }

    private sealed record BookingCommandResult(BookingResponse Response, DriverGeoSyncRequest? DriverGeoSyncRequest);

    private sealed record DriverGeoSyncRequest(
        Guid DriverId,
        DriverStatus Status,
        decimal Latitude,
        decimal Longitude,
        bool HasKnownLocation);
}
