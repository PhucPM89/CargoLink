using System.ComponentModel.DataAnnotations;

namespace CargoLink.Contracts.Bookings;

public sealed class CreateBookingRequest
{
    [Required]
    [MaxLength(150)]
    public string CustomerName { get; init; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string PickupAddress { get; init; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string DropoffAddress { get; init; } = string.Empty;

    [Range(-90, 90)]
    public decimal PickupLatitude { get; init; }

    [Range(-180, 180)]
    public decimal PickupLongitude { get; init; }

    [Range(-90, 90)]
    public decimal DropoffLatitude { get; init; }

    [Range(-180, 180)]
    public decimal DropoffLongitude { get; init; }

    [Range(0.1, 200)]
    public decimal EstimatedWeightTons { get; init; }
}
