using Workshop.Domain.Common;

namespace Workshop.Domain.Entities.Shared;

public class InsuranceCompany : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? VatNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Assessor : Entity
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LicenseNumber { get; set; }
    public Guid? InsuranceCompanyId { get; set; }
    public InsuranceCompany? InsuranceCompany { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Adjuster : Entity
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? InsuranceCompanyId { get; set; }
    public InsuranceCompany? InsuranceCompany { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Supplier : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? VatNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
