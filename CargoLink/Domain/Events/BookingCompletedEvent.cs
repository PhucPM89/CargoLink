namespace CargoLink.Domain.Events;

public sealed record BookingCompletedEvent(
    Guid BookingId,
    string BookingNumber,
    Guid DriverId,
    DateTimeOffset CompletedAt);
