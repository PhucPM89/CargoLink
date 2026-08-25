using CargoLink.Domain.Enums;
using CargoLink.Infrastructure.Data;
using CargoLink.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;

namespace CargoLink.Services;

public sealed class DriverGeoIndexBootstrapper(
    ApplicationDbContext dbContext,
    RedisDriverGeoService redisDriverGeoService)
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly RedisDriverGeoService _redisDriverGeoService = redisDriverGeoService;

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        if (!_redisDriverGeoService.IsEnabled)
        {
            return;
        }

        var drivers = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.Status == DriverStatus.Available && x.LastLocationUpdatedAt != null)
            .Select(x => new DriverGeoIndexEntry
            {
                DriverId = x.Id,
                Latitude = x.CurrentLatitude,
                Longitude = x.CurrentLongitude
            })
            .ToListAsync(cancellationToken);

        await _redisDriverGeoService.RebuildAvailableIndexAsync(drivers, cancellationToken);
    }
}
