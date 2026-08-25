using System.ComponentModel.DataAnnotations;

namespace CargoLink.Contracts.Auth;

public sealed class LoginRequest
{
    [Required]
    public string UserName { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
