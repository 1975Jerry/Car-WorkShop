using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Assessments;

public record GetAssessmentForCaseQuery(Guid InsuranceCaseId) : IRequest<AssessmentReadDto?>;

public class GetAssessmentForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetAssessmentForCaseQuery, AssessmentReadDto?>
{
    public async Task<AssessmentReadDto?> Handle(GetAssessmentForCaseQuery q, CancellationToken ct)
    {
        return await db.Assessments.AsNoTracking()
            .Where(a => a.InsuranceCaseId == q.InsuranceCaseId)
            .Select(a => new AssessmentReadDto(
                a.Id,
                a.InsuranceCaseId,
                a.AssessmentDate,
                a.LaborCost,
                a.PartsRequired,
                a.PartsCost,
                a.PaintMaterialsCost,
                a.TotalEstimatedCost,
                a.AgreedAmount,
                a.AgreementDate,
                a.IntermediateInspection,
                a.Notes,
                a.WorkItems
                    .OrderBy(w => w.CreatedAt)
                    .Select(w => new WorkItemDto(
                        w.Id,
                        w.BodyPanelId,
                        w.BodyPanel != null ? w.BodyPanel.Code : null,
                        w.Description,
                        w.Cost_Polish,
                        w.Cost_PDR,
                        w.Cost_RemoveRefit,
                        w.Cost_Replace,
                        w.Cost_DisassembleAssemble,
                        w.Cost_Repair,
                        w.Cost_Paint,
                        w.Cost_RepairPaint,
                        w.Cost_Weld,
                        w.Cost_Other,
                        w.DiscountPct,
                        w.Total))
                    .ToList()))
            .FirstOrDefaultAsync(ct);
    }
}
