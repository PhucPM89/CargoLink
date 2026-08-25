using CargoLink.Constants;
using CargoLink.Domain.Entities;
using CargoLink.Domain.Enums;
using CargoLink.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CargoLink.Services;

public sealed class AppDataSeeder(
    ApplicationDbContext dbContext,
    IPasswordHasher<User> passwordHasher)
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var driver = new Driver
        {
            FullName = "Nguyen Van Tai",
            PhoneNumber = "0900000001",
            Status = DriverStatus.Available,
            CurrentLatitude = 10.823099m,
            CurrentLongitude = 106.629662m,
            LastLocationUpdatedAt = DateTimeOffset.UtcNow
        };

        var vehicle = new Vehicle
        {
            PlateNumber = "51D-12345",
            ContainerCode = "CONT-001",
            Type = "40FT",
            CapacityTons = 28m,
            Driver = driver
        };

        var dispatcher = new User
        {
            UserName = "dispatcher",
            Role = Roles.Dispatcher
        };
        dispatcher.PasswordHash = _passwordHasher.HashPassword(dispatcher, "CargoLink123!");

        var driverUser = new User
        {
            UserName = "driver01",
            Role = Roles.Driver,
            Driver = driver
        };
        driverUser.PasswordHash = _passwordHasher.HashPassword(driverUser, "CargoLink123!");

        _dbContext.AddRange(driver, vehicle, dispatcher, driverUser);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
