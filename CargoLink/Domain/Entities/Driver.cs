using CargoLink.Domain.Enums;

namespace CargoLink.Domain.Entities;

public class Driver
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FullName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public DriverStatus Status { get; set; } = DriverStatus.Available;

    public decimal CurrentLatitude { get; set; }

    public decimal CurrentLongitude { get; set; }

    public DateTimeOffset? LastLocationUpdatedAt { get; set; }

    public Vehicle? Vehicle { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
