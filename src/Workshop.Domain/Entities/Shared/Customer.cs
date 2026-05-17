using Workshop.Domain.Common;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Shared;

public class Customer : Entity
{
    public CustomerType CustomerType { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? VatNumber { get; set; }
    public string? TaxOffice { get; set; }
    public string? IdNumber { get; set; }
    public string MobilePhone { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public bool GdprConsent { get; set; }
    public DateTime? GdprConsentAt { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
