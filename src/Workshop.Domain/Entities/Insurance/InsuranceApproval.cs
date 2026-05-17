using Workshop.Domain.Common;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Insurance;

public class InsuranceApproval : Entity
{
    public Guid InsuranceCaseId { get; set; }
    public InsuranceCase InsuranceCase { get; set; } = null!;
    public Guid InsuranceCompanyId { get; set; }
    public InsuranceCompany InsuranceCompany { get; set; } = null!;
    public bool LiabilityAccepted { get; set; }
    public bool CustomerParticipation { get; set; }
    public decimal? ParticipationAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public DateOnly ApprovalDate { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public string? Notes { get; set; }
}
