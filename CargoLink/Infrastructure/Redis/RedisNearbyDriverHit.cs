namespace CargoLink.Infrastructure.Redis;

public sealed class RedisNearbyDriverHit
{
    public Guid DriverId { get; init; }

    public decimal DistanceKm { get; init; }
}
