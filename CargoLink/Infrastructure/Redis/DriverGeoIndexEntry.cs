namespace CargoLink.Infrastructure.Redis;

public sealed class DriverGeoIndexEntry
{
    public Guid DriverId { get; init; }

    public decimal Latitude { get; init; }

    public decimal Longitude { get; init; }
}
