using CargoLink.Abstractions;
using CargoLink.Constants;
using CargoLink.Contracts.Auth;
using CargoLink.Domain.Entities;
using CargoLink.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CargoLink.Services;

public sealed class AuthService(
    ApplicationDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService jwtTokenService)
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var userName = request.UserName.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(x => x.UserName == userName, cancellationToken))
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var role = NormalizeRole(request.Role);
        Driver? driver = null;
        if (role == Roles.Driver)
        {
            if (!request.DriverId.HasValue)
            {
                throw new InvalidOperationException("Driver users must be linked to a driver record.");
            }

            driver = await _dbContext.Drivers.FirstOrDefaultAsync(x => x.Id == request.DriverId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Driver not found.");
        }

        var user = new User
        {
            UserName = userName,
            Role = role,
            DriverId = driver?.Id,
            Driver = driver
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var userName = request.UserName.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid username or password.");

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        return ToAuthResponse(user);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        return new CurrentUserResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            Role = user.Role,
            DriverId = user.DriverId
        };
    }

    private AuthResponse ToAuthResponse(User user)
    {
        var token = _jwtTokenService.CreateToken(user);
        return new AuthResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            Role = user.Role,
            DriverId = user.DriverId,
            AccessToken = token.AccessToken,
            ExpiresAt = token.ExpiresAt
        };
    }

    private static string NormalizeRole(string role)
    {
        if (string.Equals(role, Roles.Dispatcher, StringComparison.OrdinalIgnoreCase))
        {
            return Roles.Dispatcher;
        }

        if (string.Equals(role, Roles.Driver, StringComparison.OrdinalIgnoreCase))
        {
            return Roles.Driver;
        }

        throw new InvalidOperationException("Role must be Dispatcher or Driver.");
    }
}
