using Workshop.Domain.Common;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Retail;

public class RetailCase : Entity, IBranchScoped
{
    public string CaseNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public RetailCaseStatus Status { get; set; } = RetailCaseStatus.Quoted;
    public Guid? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }
    public string WorkType { get; set; } = string.Empty;
    public decimal FinalCost { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalWithVat { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    public RetailRepair? Repair { get; set; }
    public ICollection<RetailPartLine> PartLines { get; set; } = new List<RetailPartLine>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class RetailPartLine : Entity
{
    public Guid RetailCaseId { get; set; }
    public RetailCase RetailCase { get; set; } = null!;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid DestinationBranchId { get; set; }
    public Branch DestinationBranch { get; set; } = null!;
    public PartType PartType { get; set; }
    public string PartName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Total { get; set; }
    public PartReceivedStatus ReceivedStatus { get; set; } = PartReceivedStatus.Pending;
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string? StorageLocation { get; set; }
    public string? Notes { get; set; }
}

public class RetailRepair : Entity
{
    public Guid RetailCaseId { get; set; }
    public RetailCase RetailCase { get; set; } = null!;
    public DateOnly ScheduledDate { get; set; }
    public TimeOnly? ScheduledTime { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public Guid? TechnicianId { get; set; }
    public User? Technician { get; set; }
    public RepairStatus Status { get; set; } = RepairStatus.Scheduled;
}
