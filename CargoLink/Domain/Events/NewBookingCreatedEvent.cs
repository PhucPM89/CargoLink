namespace CargoLink.Domain.Events;

public sealed record NewBookingCreatedEvent(
    Guid BookingId,
    string BookingNumber,
    string CustomerName,
    string PickupAddress,
    string DropoffAddress,
    DateTimeOffset CreatedAt);
