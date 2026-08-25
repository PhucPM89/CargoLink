namespace CargoLink.Infrastructure.Redis;

public sealed class RedisGeoOptions
{
    public const string SectionName = "RedisGeo";

    public string AvailableDriversKey { get; init; } = "drivers:available:geo";

    public int NearbySearchTake { get; init; } = 10;
}
