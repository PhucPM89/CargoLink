namespace CargoLink.Contracts.Drivers;

public sealed class DriverLocationResponse
{
    public Guid DriverId { get; init; }

    public string DriverName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public decimal Latitude { get; init; }

    public decimal Longitude { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
