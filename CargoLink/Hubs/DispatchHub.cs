using CargoLink.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CargoLink.Hubs;

[Authorize(Roles = $"{Roles.Dispatcher},{Roles.Driver}")]
public class DispatchHub : Hub
{
    public const string DispatchersGroup = "dispatchers";

    public Task JoinDispatcherBoard()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, DispatchersGroup);
    }

    public Task LeaveDispatcherBoard()
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, DispatchersGroup);
    }

    public Task JoinBooking(Guid bookingId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GetBookingGroup(bookingId));
    }

    public Task LeaveBooking(Guid bookingId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetBookingGroup(bookingId));
    }

    public static string GetBookingGroup(Guid bookingId)
    {
        return $"booking:{bookingId}";
    }
}
