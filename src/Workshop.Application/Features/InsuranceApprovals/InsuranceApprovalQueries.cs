using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.InsuranceApprovals;

public record GetInsuranceApprovalForCaseQuery(Guid InsuranceCaseId) : IRequest<InsuranceApprovalReadDto?>;

public class GetInsuranceApprovalForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetInsuranceApprovalForCaseQuery, InsuranceApprovalReadDto?>
{
    public async Task<InsuranceApprovalReadDto?> Handle(GetInsuranceApprovalForCaseQuery q, CancellationToken ct) =>
        await db.InsuranceApprovals.AsNoTracking()
            .Where(a => a.InsuranceCaseId == q.InsuranceCaseId)
            .Select(a => new InsuranceApprovalReadDto(
                a.Id,
                a.InsuranceCaseId,
                a.InsuranceCompanyId,
                a.InsuranceCompany.Name,
                a.LiabilityAccepted,
                a.CustomerParticipation,
                a.ParticipationAmount,
                a.ApprovedAmount,
                a.ApprovalDate,
                a.ApprovalStatus,
                a.Notes,
                a.UpdatedAt))
            .FirstOrDefaultAsync(ct);
}
