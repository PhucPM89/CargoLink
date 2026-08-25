using CargoLink.Domain.Enums;

namespace CargoLink.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string BookingNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string PickupAddress { get; set; } = string.Empty;

    public string DropoffAddress { get; set; } = string.Empty;

    public decimal PickupLatitude { get; set; }

    public decimal PickupLongitude { get; set; }

    public decimal DropoffLatitude { get; set; }

    public decimal DropoffLongitude { get; set; }

    public decimal EstimatedWeightTons { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public Guid? DriverId { get; set; }

    public Driver? Driver { get; set; }

    public Guid? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
}
