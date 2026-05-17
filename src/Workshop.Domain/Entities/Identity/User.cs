using Microsoft.AspNetCore.Identity;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Identity;

public class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public PortalAudience PortalAudience { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? InsuranceCompanyId { get; set; }
    public InsuranceCompany? InsuranceCompany { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string Language { get; set; } = "el";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Role : IdentityRole<Guid>
{
    public Role() { }
    public Role(string name) : base(name) { }
}

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string BranchManager = "BranchManager";
    public const string Receptionist = "Receptionist";
    public const string Technician = "Technician";
    public const string BodyShopManager = "BodyShopManager";

    public static readonly string[] AllStaffRoles =
    {
        Admin, BranchManager, Receptionist, Technician, BodyShopManager
    };
}
