using CargoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CargoLink.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingStatusHistory> BookingStatusHistory => Set<BookingStatusHistory>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.HasIndex(x => x.DriverId).IsUnique();
            entity.HasOne(x => x.Driver)
                .WithOne()
                .HasForeignKey<User>(x => x.DriverId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.ToTable("drivers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.CurrentLatitude).HasPrecision(9, 6);
            entity.Property(x => x.CurrentLongitude).HasPrecision(9, 6);
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
            entity.HasIndex(x => new { x.Status, x.CurrentLatitude, x.CurrentLongitude });
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("vehicles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlateNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ContainerCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CapacityTons).HasPrecision(10, 2);
            entity.HasIndex(x => x.PlateNumber).IsUnique();
            entity.HasIndex(x => x.ContainerCode).IsUnique();
            entity.HasIndex(x => x.DriverId).IsUnique();
            entity.HasOne(x => x.Driver)
                .WithOne(x => x.Vehicle)
                .HasForeignKey<Vehicle>(x => x.DriverId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BookingNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.PickupAddress).HasMaxLength(250).IsRequired();
            entity.Property(x => x.DropoffAddress).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.PickupLatitude).HasPrecision(9, 6);
            entity.Property(x => x.PickupLongitude).HasPrecision(9, 6);
            entity.Property(x => x.DropoffLatitude).HasPrecision(9, 6);
            entity.Property(x => x.DropoffLongitude).HasPrecision(9, 6);
            entity.Property(x => x.EstimatedWeightTons).HasPrecision(10, 2);
            entity.HasIndex(x => x.BookingNumber).IsUnique();
            entity.HasIndex(x => new { x.Status, x.DriverId });
            entity.HasOne(x => x.Driver)
                .WithMany(x => x.Bookings)
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Vehicle)
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BookingStatusHistory>(entity =>
        {
            entity.ToTable("booking_status_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Note).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.BookingId, x.CreatedAt });
            entity.HasOne(x => x.Booking)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Topic).HasMaxLength(150).IsRequired();
            entity.Property(x => x.MessageKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.MessageType).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.LockId).HasMaxLength(100);
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ProcessedAt, x.LockedUntil, x.OccurredAt });
            entity.HasIndex(x => new { x.Topic, x.MessageKey });
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MessageId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Consumer).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Topic).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.LockId).HasMaxLength(100);
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.HasIndex(x => new { x.MessageId, x.Consumer }).IsUnique();
            entity.HasIndex(x => new { x.ProcessedAt, x.LockedUntil, x.ReceivedAt });
        });
    }
}
