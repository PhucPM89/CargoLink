using CargoLink.Domain.Enums;

namespace CargoLink.Domain.Entities;

public class BookingStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }

    public Booking Booking { get; set; } = null!;

    public BookingStatus Status { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
