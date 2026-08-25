using System.ComponentModel.DataAnnotations;
using CargoLink.Constants;

namespace CargoLink.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required]
    [MinLength(4)]
    [MaxLength(50)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [RegularExpression($"^({Roles.Dispatcher}|{Roles.Driver})$", ErrorMessage = "Role must be Dispatcher or Driver.")]
    public string Role { get; init; } = Roles.Dispatcher;

    public Guid? DriverId { get; init; }
}
