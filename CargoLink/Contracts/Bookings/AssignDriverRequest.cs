using System.ComponentModel.DataAnnotations;

namespace CargoLink.Contracts.Bookings;

public sealed class AssignDriverRequest
{
    [Required]
    public Guid DriverId { get; init; }
}
