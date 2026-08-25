namespace CargoLink.Contracts.Drivers;

public sealed class NearbyDriverResponse
{
    public Guid DriverId { get; init; }

    public string DriverName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string? VehiclePlateNumber { get; init; }

    public decimal Latitude { get; init; }

    public decimal Longitude { get; init; }

    public decimal DistanceKm { get; init; }
}
