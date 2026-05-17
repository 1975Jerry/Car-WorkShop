using Workshop.Domain.Common;

namespace Workshop.Domain.Entities.Shared;

public class Branch : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public Warehouse? Warehouse { get; set; }
}

public class Warehouse : Entity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
