using CargoLink.Contracts.Drivers;
using CargoLink.Domain.Enums;
using CargoLink.Hubs;
using CargoLink.Infrastructure.Data;
using CargoLink.Infrastructure.Redis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CargoLink.Services;

public sealed class DriverService(
    ApplicationDbContext dbContext,
    RedisDriverGeoService redisDriverGeoService,
    IHubContext<DispatchHub> hubContext)
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly RedisDriverGeoService _redisDriverGeoService = redisDriverGeoService;
    private readonly IHubContext<DispatchHub> _hubContext = hubContext;

    public async Task<IReadOnlyList<NearbyDriverResponse>> GetNearbyDriversAsync(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        CancellationToken cancellationToken = default)
    {
        var geoHits = await _redisDriverGeoService.SearchAvailableDriversAsync(latitude, longitude, radiusKm, cancellationToken: cancellationToken);
        if (geoHits is not null)
        {
            return await GetNearbyDriversFromRedisHitsAsync(geoHits, cancellationToken);
        }

        // Redis GEO is unavailable, so fall back to MySQL range filtering.
        var latitudeRange = radiusKm / 111m;
        var cosine = (decimal)Math.Max(0.1d, Math.Cos((double)latitude * Math.PI / 180d));
        var longitudeRange = radiusKm / (111m * cosine);

        var candidates = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.Status == DriverStatus.Available
                && x.CurrentLatitude >= latitude - latitudeRange
                && x.CurrentLatitude <= latitude + latitudeRange
                && x.CurrentLongitude >= longitude - longitudeRange
                && x.CurrentLongitude <= longitude + longitudeRange)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.PhoneNumber,
                x.CurrentLatitude,
                x.CurrentLongitude,
                VehiclePlateNumber = x.Vehicle != null ? x.Vehicle.PlateNumber : null
            })
            .ToListAsync(cancellationToken);

        var result = candidates
            .Select(x => new NearbyDriverResponse
            {
                DriverId = x.Id,
                DriverName = x.FullName,
                PhoneNumber = x.PhoneNumber,
                VehiclePlateNumber = x.VehiclePlateNumber,
                Latitude = x.CurrentLatitude,
                Longitude = x.CurrentLongitude,
                DistanceKm = Math.Round(CalculateDistanceKm(latitude, longitude, x.CurrentLatitude, x.CurrentLongitude), 2)
            })
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .Take(10)
            .ToList();

        return result;
    }

    public async Task<DriverLocationResponse> UpdateLocationAsync(
        Guid driverId,
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var driver = await _dbContext.Drivers
            .FirstOrDefaultAsync(x => x.Id == driverId, cancellationToken)
            ?? throw new KeyNotFoundException("Driver not found.");

        driver.CurrentLatitude = request.Latitude;
        driver.CurrentLongitude = request.Longitude;
        driver.LastLocationUpdatedAt = DateTimeOffset.UtcNow;

        if (request.Status.HasValue)
        {
            driver.Status = request.Status.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _redisDriverGeoService.UpdateDriverLocationAsync(
            driver.Id,
            driver.CurrentLatitude,
            driver.CurrentLongitude,
            driver.Status,
            cancellationToken);

        var response = new DriverLocationResponse
        {
            DriverId = driver.Id,
            DriverName = driver.FullName,
            Status = driver.Status.ToString(),
            Latitude = driver.CurrentLatitude,
            Longitude = driver.CurrentLongitude,
            UpdatedAt = driver.LastLocationUpdatedAt ?? DateTimeOffset.UtcNow
        };

        await _hubContext.Clients.Group(DispatchHub.DispatchersGroup)
            .SendAsync("driverLocationUpdated", response, cancellationToken);

        if (request.ActiveBookingId.HasValue)
        {
            await _hubContext.Clients.Group(DispatchHub.GetBookingGroup(request.ActiveBookingId.Value))
                .SendAsync("driverLocationUpdated", response, cancellationToken);
        }

        return response;
    }

    private async Task<IReadOnlyList<NearbyDriverResponse>> GetNearbyDriversFromRedisHitsAsync(
        IReadOnlyList<RedisNearbyDriverHit> geoHits,
        CancellationToken cancellationToken)
    {
        if (geoHits.Count == 0)
        {
            return [];
        }

        var distanceByDriverId = geoHits.ToDictionary(x => x.DriverId, x => x.DistanceKm);
        var driverIds = geoHits.Select(x => x.DriverId).ToHashSet();

        var drivers = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => driverIds.Contains(x.Id) && x.Status == DriverStatus.Available)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.PhoneNumber,
                x.CurrentLatitude,
                x.CurrentLongitude,
                VehiclePlateNumber = x.Vehicle != null ? x.Vehicle.PlateNumber : null
            })
            .ToListAsync(cancellationToken);

        var driverLookup = drivers.ToDictionary(x => x.Id);
        return geoHits
            .Where(x => driverLookup.ContainsKey(x.DriverId))
            .Select(x =>
            {
                var driver = driverLookup[x.DriverId];
                return new NearbyDriverResponse
                {
                    DriverId = driver.Id,
                    DriverName = driver.FullName,
                    PhoneNumber = driver.PhoneNumber,
                    VehiclePlateNumber = driver.VehiclePlateNumber,
                    Latitude = driver.CurrentLatitude,
                    Longitude = driver.CurrentLongitude,
                    DistanceKm = Math.Round(distanceByDriverId[driver.Id], 2)
                };
            })
            .ToList();
    }

    private static decimal CalculateDistanceKm(
        decimal fromLatitude,
        decimal fromLongitude,
        decimal toLatitude,
        decimal toLongitude)
    {
        const double earthRadiusKm = 6371d;
        static double ToRadians(double angle) => angle * Math.PI / 180d;

        var dLatitude = ToRadians((double)(toLatitude - fromLatitude));
        var dLongitude = ToRadians((double)(toLongitude - fromLongitude));
        var originLatitude = ToRadians((double)fromLatitude);
        var destinationLatitude = ToRadians((double)toLatitude);

        var a = Math.Pow(Math.Sin(dLatitude / 2d), 2d)
            + Math.Cos(originLatitude) * Math.Cos(destinationLatitude) * Math.Pow(Math.Sin(dLongitude / 2d), 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return (decimal)(earthRadiusKm * c);
    }
}
