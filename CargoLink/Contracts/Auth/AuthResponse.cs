namespace CargoLink.Contracts.Auth;

public sealed class AuthResponse
{
    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public Guid? DriverId { get; init; }

    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }
}
