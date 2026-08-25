using System.Globalization;
using CargoLink.Domain.Enums;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CargoLink.Infrastructure.Redis;

public sealed class RedisDriverGeoService(
    IServiceProvider serviceProvider,
    IOptions<RedisGeoOptions> options,
    ILogger<RedisDriverGeoService> logger)
{
    private readonly IConnectionMultiplexer? _redis = serviceProvider.GetService<IConnectionMultiplexer>();
    private readonly RedisGeoOptions _options = options.Value;
    private readonly ILogger<RedisDriverGeoService> _logger = logger;

    public bool IsEnabled => _redis is not null;

    public async Task<IReadOnlyList<RedisNearbyDriverHit>?> SearchAvailableDriversAsync(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_redis is null)
        {
            return null;
        }

        try
        {
            var db = _redis.GetDatabase();
            var count = Math.Max(1, take ?? _options.NearbySearchTake);
            var result = await db.ExecuteAsync(
                "GEOSEARCH",
                _options.AvailableDriversKey,
                "FROMLONLAT",
                ToRedisNumber(longitude),
                ToRedisNumber(latitude),
                "BYRADIUS",
                ToRedisNumber(radiusKm),
                "KM",
                "ASC",
                "COUNT",
                count.ToString(CultureInfo.InvariantCulture),
                "WITHDIST");

            return ParseGeoSearchResult(result);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis GEO search failed for nearby drivers.");
            return null;
        }
    }

    public async Task UpdateDriverLocationAsync(
        Guid driverId,
        decimal latitude,
        decimal longitude,
        DriverStatus status,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_redis is null)
        {
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            var member = driverId.ToString();
            if (status == DriverStatus.Available)
            {
                await db.ExecuteAsync(
                    "GEOADD",
                    _options.AvailableDriversKey,
                    ToRedisNumber(longitude),
                    ToRedisNumber(latitude),
                    member);
                return;
            }

            await db.SortedSetRemoveAsync(_options.AvailableDriversKey, member);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis GEO update failed for driver {DriverId}.", driverId);
        }
    }

    public async Task RebuildAvailableIndexAsync(
        IReadOnlyCollection<DriverGeoIndexEntry> drivers,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_redis is null)
        {
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(_options.AvailableDriversKey);

            foreach (var driver in drivers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await db.ExecuteAsync(
                    "GEOADD",
                    _options.AvailableDriversKey,
                    ToRedisNumber(driver.Longitude),
                    ToRedisNumber(driver.Latitude),
                    driver.DriverId.ToString());
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis GEO rebuild failed for available drivers index.");
        }
    }

    private static List<RedisNearbyDriverHit> ParseGeoSearchResult(RedisResult result)
    {
        if (result.IsNull)
        {
            return [];
        }

        var rows = (RedisResult[]?)result;
        if (rows is null || rows.Length == 0)
        {
            return [];
        }

        var hits = new List<RedisNearbyDriverHit>(rows.Length);

        foreach (var row in rows)
        {
            var values = (RedisResult[]?)row;
            if (values is null || values.Length < 2)
            {
                continue;
            }

            var member = values[0].ToString();
            var distanceText = values[1].ToString();
            if (!Guid.TryParse(member, out var driverId))
            {
                continue;
            }

            if (!decimal.TryParse(distanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var distanceKm))
            {
                continue;
            }

            hits.Add(new RedisNearbyDriverHit
            {
                DriverId = driverId,
                DistanceKm = distanceKm
            });
        }

        return hits;
    }

    private static string ToRedisNumber(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
