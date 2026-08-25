using CargoLink.Constants;
using CargoLink.Contracts.Drivers;
using CargoLink.Extensions;
using CargoLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CargoLink.Controllers;

[ApiController]
[Authorize]
[Route("api/drivers")]
public sealed class DriversController(DriverService driverService) : ControllerBase
{
    private readonly DriverService _driverService = driverService;

    [Authorize(Roles = Roles.Dispatcher)]
    [HttpGet("nearby")]
    public async Task<ActionResult<IReadOnlyList<NearbyDriverResponse>>> GetNearby(
        [FromQuery] decimal latitude,
        [FromQuery] decimal longitude,
        [FromQuery] decimal radiusKm = 25,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _driverService.GetNearbyDriversAsync(latitude, longitude, radiusKm, cancellationToken));
    }

    [Authorize(Roles = $"{Roles.Dispatcher},{Roles.Driver}")]
    [HttpPut("{driverId:guid}/location")]
    public async Task<ActionResult<DriverLocationResponse>> UpdateLocation(
        Guid driverId,
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(Roles.Driver))
        {
            var currentDriverId = User.GetDriverId();
            if (!currentDriverId.HasValue || currentDriverId.Value != driverId)
            {
                return Forbid();
            }
        }

        try
        {
            return Ok(await _driverService.UpdateLocationAsync(driverId, request, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }
}
