namespace CargoLink.Contracts.Bookings;

public sealed class BookingResponse
{
    public Guid Id { get; init; }

    public string BookingNumber { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string PickupAddress { get; init; } = string.Empty;

    public string DropoffAddress { get; init; } = string.Empty;

    public decimal PickupLatitude { get; init; }

    public decimal PickupLongitude { get; init; }

    public decimal DropoffLatitude { get; init; }

    public decimal DropoffLongitude { get; init; }

    public decimal EstimatedWeightTons { get; init; }

    public string Status { get; init; } = string.Empty;

    public Guid? DriverId { get; init; }

    public string? DriverName { get; init; }

    public Guid? VehicleId { get; init; }

    public string? VehiclePlateNumber { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}
