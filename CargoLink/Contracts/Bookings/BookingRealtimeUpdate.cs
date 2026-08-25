namespace CargoLink.Contracts.Bookings;

public sealed class BookingRealtimeUpdate
{
    public Guid BookingId { get; init; }

    public string BookingNumber { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid? DriverId { get; init; }

    public string? DriverName { get; init; }

    public string? VehiclePlateNumber { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
