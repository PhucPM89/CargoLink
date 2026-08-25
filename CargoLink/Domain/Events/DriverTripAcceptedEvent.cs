namespace CargoLink.Domain.Events;

public sealed record DriverTripAcceptedEvent(
    Guid BookingId,
    string BookingNumber,
    Guid DriverId,
    string DriverName,
    string VehiclePlateNumber,
    DateTimeOffset AcceptedAt);
