using CargoLink.Domain.Entities;
using CargoLink.Infrastructure.Auth;

namespace CargoLink.Abstractions;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(User user);
}
