using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Repairs;

public record GetRepairForCaseQuery(Guid InsuranceCaseId) : IRequest<RepairDto?>;

public class GetRepairForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetRepairForCaseQuery, RepairDto?>
{
    public async Task<RepairDto?> Handle(GetRepairForCaseQuery q, CancellationToken ct) =>
        await db.Repairs.AsNoTracking()
            .Where(r => r.InsuranceCaseId == q.InsuranceCaseId)
            .Select(r => new RepairDto(
                r.Id,
                r.InsuranceCaseId,
                r.ScheduledDate,
                r.ScheduledTime,
                r.StartDate,
                r.CompletionDate,
                r.TechnicianId,
                r.Technician != null ? r.Technician.FullName : null,
                r.Status,
                r.IntermediateInspectionDone,
                r.Notes,
                r.UpdatedAt))
            .FirstOrDefaultAsync(ct);
}
