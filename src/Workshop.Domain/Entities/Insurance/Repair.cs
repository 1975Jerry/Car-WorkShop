using Workshop.Domain.Common;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Insurance;

public class Repair : Entity
{
    public Guid InsuranceCaseId { get; set; }
    public InsuranceCase InsuranceCase { get; set; } = null!;
    public DateOnly ScheduledDate { get; set; }
    public TimeOnly? ScheduledTime { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public Guid? TechnicianId { get; set; }
    public User? Technician { get; set; }
    public RepairStatus Status { get; set; } = RepairStatus.Scheduled;
    public bool IntermediateInspectionDone { get; set; }
    public string? Notes { get; set; }

    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
}
