using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.RetailRepairs;

public record GetRetailRepairForCaseQuery(Guid RetailCaseId) : IRequest<RetailRepairDto?>;

public class GetRetailRepairForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetRetailRepairForCaseQuery, RetailRepairDto?>
{
    public async Task<RetailRepairDto?> Handle(GetRetailRepairForCaseQuery q, CancellationToken ct)
    {
        var raw = await db.RetailRepairs.AsNoTracking()
            .Where(r => r.RetailCaseId == q.RetailCaseId)
            .Select(r => new
            {
                r.Id,
                r.RetailCaseId,
                r.ScheduledDate,
                r.ScheduledTime,
                r.StartDate,
                r.CompletionDate,
                r.TechnicianId,
                r.Status,
                r.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (raw is null) return null;

        string? technicianName = null;
        if (raw.TechnicianId.HasValue)
        {
            technicianName = await db.Users.AsNoTracking()
                .Where(u => u.Id == raw.TechnicianId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct);
        }

        return new RetailRepairDto(
            raw.Id, raw.RetailCaseId, raw.ScheduledDate, raw.ScheduledTime,
            raw.StartDate, raw.CompletionDate, raw.TechnicianId, technicianName,
            raw.Status, raw.UpdatedAt);
    }
}
