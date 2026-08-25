namespace CargoLink.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PlateNumber { get; set; } = string.Empty;

    public string ContainerCode { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public decimal CapacityTons { get; set; }

    public Guid DriverId { get; set; }

    public Driver Driver { get; set; } = null!;
}
