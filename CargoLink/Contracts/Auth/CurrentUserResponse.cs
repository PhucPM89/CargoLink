namespace CargoLink.Contracts.Auth;

public sealed class CurrentUserResponse
{
    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public Guid? DriverId { get; init; }
}
