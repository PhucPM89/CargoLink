using System.ComponentModel.DataAnnotations;
using CargoLink.Domain.Enums;

namespace CargoLink.Contracts.Drivers;

public sealed class UpdateDriverLocationRequest
{
    [Range(-90, 90)]
    public decimal Latitude { get; init; }

    [Range(-180, 180)]
    public decimal Longitude { get; init; }

    public DriverStatus? Status { get; init; }

    public Guid? ActiveBookingId { get; init; }
}
