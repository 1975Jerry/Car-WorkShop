using Workshop.Domain.Common;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Shared;

public class Vehicle : Entity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string PlateNumber { get; set; } = string.Empty;
    public string? Vin { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Version { get; set; }
    public int? Year { get; set; }
    public string? Color { get; set; }
    public FuelType? FuelType { get; set; }
    public int? Mileage { get; set; }
    public Guid? InsuranceCompanyId { get; set; }
    public InsuranceCompany? InsuranceCompany { get; set; }
    public string? PolicyNumber { get; set; }
    public DateOnly? InsuranceExpiration { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
