using CargoLink.Constants;
using CargoLink.Contracts.Bookings;
using CargoLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CargoLink.Controllers;

[ApiController]
[Authorize(Roles = Roles.Dispatcher)]
[Route("api/bookings")]
public sealed class BookingsController(BookingService bookingService) : ControllerBase
{
    private readonly BookingService _bookingService = bookingService;

    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> GetActive(CancellationToken cancellationToken)
    {
        return Ok(await _bookingService.GetActiveBookingsAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _bookingService.CreateBookingAsync(request, cancellationToken));
    }

    [HttpPost("{bookingId:guid}/assign-driver")]
    public async Task<ActionResult<BookingResponse>> AssignDriver(
        Guid bookingId,
        AssignDriverRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _bookingService.AssignDriverAsync(bookingId, request.DriverId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{bookingId:guid}/complete")]
    public async Task<ActionResult<BookingResponse>> Complete(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _bookingService.CompleteBookingAsync(bookingId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
