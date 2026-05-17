using Workshop.Domain.Common;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Insurance;

public class Photo : Entity
{
    public Guid? AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }
    public Guid? RepairId { get; set; }
    public Repair? Repair { get; set; }
    public Guid? RetailRepairId { get; set; }
    public RetailRepair? RetailRepair { get; set; }
    public PhotoPhase Phase { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
}
