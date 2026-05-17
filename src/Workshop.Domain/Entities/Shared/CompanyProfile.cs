using Workshop.Domain.Common;

namespace Workshop.Domain.Entities.Shared;

public class CompanyProfile : Entity
{
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string VatNumber { get; set; } = string.Empty;
    public string? TaxOffice { get; set; }
    public string? LogoPath { get; set; }
    public decimal DefaultVatRate { get; set; } = 24.00m;
}
