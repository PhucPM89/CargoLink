using CargoLink.Infrastructure.Data;
using CargoLink.Infrastructure.Events;
using CargoLink.Services;
using Microsoft.EntityFrameworkCore;

namespace CargoLink.Extensions;

public static class WebApplicationExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await EnsureInboxSchemaAsync(dbContext);

        var seeder = scope.ServiceProvider.GetRequiredService<AppDataSeeder>();
        await seeder.SeedAsync();

        var geoIndexBootstrapper = scope.ServiceProvider.GetRequiredService<DriverGeoIndexBootstrapper>();
        await geoIndexBootstrapper.WarmupAsync();

        var kafkaTopicInitializer = scope.ServiceProvider.GetRequiredService<KafkaTopicInitializer>();
        await kafkaTopicInitializer.EnsureTopicsExistAsync();
    }

    private static Task EnsureInboxSchemaAsync(ApplicationDbContext dbContext)
    {
        return dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `inbox_messages` (
                `Id` char(36) NOT NULL,
                `MessageId` varchar(120) NOT NULL,
                `Consumer` varchar(120) NOT NULL,
                `Topic` varchar(150) NOT NULL,
                `Payload` longtext NOT NULL,
                `ReceivedAt` datetime(6) NOT NULL,
                `LastAttemptAt` datetime(6) NULL,
                `ProcessedAt` datetime(6) NULL,
                `LockedUntil` datetime(6) NULL,
                `AttemptCount` int NOT NULL,
                `LockId` varchar(100) NULL,
                `LastError` varchar(2000) NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `UX_inbox_messages_message_id_consumer` (`MessageId`, `Consumer`),
                KEY `IX_inbox_messages_processed_locked_received` (`ProcessedAt`, `LockedUntil`, `ReceivedAt`)
            );
            """);
    }
}
