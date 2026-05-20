using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.RetailCasePanels;

public record RetailCasePanelDto(
    Guid Id,
    Guid BodyPanelId,
    string Code,
    string DescriptionGr,
    BodyPanelCategory Category,
    PanelSide Side,
    decimal? DiagramX,
    decimal? DiagramY,
    string? Notes);

public record GetRetailCasePanelsQuery(Guid RetailCaseId)
    : IRequest<IReadOnlyList<RetailCasePanelDto>>;

public class GetRetailCasePanelsHandler(IWorkshopDbContext db)
    : IRequestHandler<GetRetailCasePanelsQuery, IReadOnlyList<RetailCasePanelDto>>
{
    public async Task<IReadOnlyList<RetailCasePanelDto>> Handle(GetRetailCasePanelsQuery q, CancellationToken ct)
    {
        // Explicit join — avoids the EF InMemory required-nav projection pitfall noted in the project memory.
        return await (
            from rcp in db.RetailCasePanels.AsNoTracking()
            join p in db.BodyPanels.AsNoTracking() on rcp.BodyPanelId equals p.Id
            where rcp.RetailCaseId == q.RetailCaseId
            orderby p.Side, p.Code
            select new RetailCasePanelDto(
                rcp.Id, p.Id, p.Code, p.DescriptionGr,
                p.Category, p.Side, p.DiagramX, p.DiagramY, rcp.Notes))
            .ToListAsync(ct);
    }
}

public record AddRetailCasePanelCommand(Guid RetailCaseId, Guid BodyPanelId) : IRequest<Guid>;

public class AddRetailCasePanelHandler(IWorkshopDbContext db)
    : IRequestHandler<AddRetailCasePanelCommand, Guid>
{
    public async Task<Guid> Handle(AddRetailCasePanelCommand cmd, CancellationToken ct)
    {
        // Idempotent: if already linked, return the existing row.
        var existing = await db.RetailCasePanels
            .FirstOrDefaultAsync(x => x.RetailCaseId == cmd.RetailCaseId && x.BodyPanelId == cmd.BodyPanelId, ct);
        if (existing is not null) return existing.Id;

        var row = new RetailCasePanel
        {
            RetailCaseId = cmd.RetailCaseId,
            BodyPanelId = cmd.BodyPanelId
        };
        db.RetailCasePanels.Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }
}

public record RemoveRetailCasePanelCommand(Guid RetailCaseId, Guid BodyPanelId) : IRequest<Unit>;

public class RemoveRetailCasePanelHandler(IWorkshopDbContext db)
    : IRequestHandler<RemoveRetailCasePanelCommand, Unit>
{
    public async Task<Unit> Handle(RemoveRetailCasePanelCommand cmd, CancellationToken ct)
    {
        var row = await db.RetailCasePanels
            .FirstOrDefaultAsync(x => x.RetailCaseId == cmd.RetailCaseId && x.BodyPanelId == cmd.BodyPanelId, ct);
        if (row is not null)
        {
            db.RetailCasePanels.Remove(row);
            await db.SaveChangesAsync(ct);
        }
        return Unit.Value;
    }
}

public record UpdateRetailCasePanelNotesCommand(Guid RetailCasePanelId, string? Notes) : IRequest<Unit>;

public class UpdateRetailCasePanelNotesHandler(IWorkshopDbContext db)
    : IRequestHandler<UpdateRetailCasePanelNotesCommand, Unit>
{
    public async Task<Unit> Handle(UpdateRetailCasePanelNotesCommand cmd, CancellationToken ct)
    {
        var row = await db.RetailCasePanels
            .FirstOrDefaultAsync(x => x.Id == cmd.RetailCasePanelId, ct);
        if (row is null) return Unit.Value;
        row.Notes = string.IsNullOrWhiteSpace(cmd.Notes) ? null : cmd.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
