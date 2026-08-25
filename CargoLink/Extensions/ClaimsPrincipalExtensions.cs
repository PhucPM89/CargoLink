using System.Security.Claims;

namespace CargoLink.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id is missing.");
        }

        return userId;
    }

    public static Guid? GetDriverId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("driver_id");
        return Guid.TryParse(value, out var driverId) ? driverId : null;
    }
}
