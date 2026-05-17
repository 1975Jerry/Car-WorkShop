using Workshop.Domain.Common;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Insurance;

public class Assessment : Entity
{
    public Guid InsuranceCaseId { get; set; }
    public InsuranceCase InsuranceCase { get; set; } = null!;
    public DateOnly AssessmentDate { get; set; }
    public decimal LaborCost { get; set; }
    public bool PartsRequired { get; set; }
    public decimal? PartsCost { get; set; }
    public decimal? PaintMaterialsCost { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public decimal AgreedAmount { get; set; }
    public DateOnly AgreementDate { get; set; }
    public bool IntermediateInspection { get; set; }
    public string? Notes { get; set; }

    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    public ICollection<InsurancePartLine> PartLines { get; set; } = new List<InsurancePartLine>();
    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
}

public class WorkItem : Entity
{
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    public Guid? BodyPanelId { get; set; }
    public BodyPanel? BodyPanel { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? Cost_Polish { get; set; }
    public decimal? Cost_PDR { get; set; }
    public decimal? Cost_RemoveRefit { get; set; }
    public decimal? Cost_Replace { get; set; }
    public decimal? Cost_DisassembleAssemble { get; set; }
    public decimal? Cost_Repair { get; set; }
    public decimal? Cost_Paint { get; set; }
    public decimal? Cost_RepairPaint { get; set; }
    public decimal? Cost_Weld { get; set; }
    public decimal? Cost_Other { get; set; }
    public decimal? DiscountPct { get; set; }
    public decimal Total { get; set; }
}

public class InsurancePartLine : Entity
{
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid DestinationBranchId { get; set; }
    public Branch DestinationBranch { get; set; } = null!;
    public PartType PartType { get; set; }
    public string PartName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal? DiscountPct { get; set; }
    public decimal Total { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; }
    public bool InsuranceApproved { get; set; }
    public bool Ordered { get; set; }
    public DateOnly? OrderDate { get; set; }
    public PartReceivedStatus ReceivedStatus { get; set; } = PartReceivedStatus.Pending;
    public DateOnly? ReceivedDate { get; set; }
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string? StorageLocation { get; set; }
    public string? Notes { get; set; }
}
