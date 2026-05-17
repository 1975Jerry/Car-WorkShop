using Workshop.Domain.Common;
using Workshop.Domain.Entities.CrossCutting;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Insurance;

public class InsuranceCase : Entity, IBranchScoped
{
    public string CaseNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid InsuranceCompanyId { get; set; }
    public InsuranceCompany InsuranceCompany { get; set; } = null!;
    public string? ClaimNumber { get; set; }
    public InsuranceCaseStatus Status { get; set; } = InsuranceCaseStatus.NewCase;
    public CasePriority? Priority { get; set; }
    public Guid? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }
    public Guid? AssessorId { get; set; }
    public Assessor? Assessor { get; set; }
    public Guid? AdjusterId { get; set; }
    public Adjuster? Adjuster { get; set; }
    public string? DriverFirstName { get; set; }
    public string? DriverLastName { get; set; }
    public string? DriverPhone { get; set; }
    public string? DriverEmail { get; set; }
    public DateOnly? AccidentDate { get; set; }
    public int? MileageAtAssessment { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }

    public Assessment? Assessment { get; set; }
    public InsuranceApproval? Approval { get; set; }
    public Repair? Repair { get; set; }
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<CaseEvent> Events { get; set; } = new List<CaseEvent>();
}
