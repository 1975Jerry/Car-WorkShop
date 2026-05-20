using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.BodyPanels;

public record BodyPanelDto(
    Guid Id,
    string Code,
    string DescriptionGr,
    string? DescriptionEn,
    BodyPanelCategory Category,
    PanelSide Side,
    decimal? DiagramX,
    decimal? DiagramY,
    IReadOnlyList<OperationType> AllowedOperations);

public record GetBodyPanelCatalogQuery() : IRequest<IReadOnlyList<BodyPanelDto>>;

public class GetBodyPanelCatalogHandler(IWorkshopDbContext db)
    : IRequestHandler<GetBodyPanelCatalogQuery, IReadOnlyList<BodyPanelDto>>
{
    public async Task<IReadOnlyList<BodyPanelDto>> Handle(GetBodyPanelCatalogQuery _, CancellationToken ct)
    {
        var rows = await db.BodyPanels.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.Code)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.DescriptionGr,
                p.DescriptionEn,
                p.Category,
                p.Side,
                p.DiagramX,
                p.DiagramY,
                Ops = p.AllowedOperations.Select(o => o.Operation).ToList()
            })
            .ToListAsync(ct);

        return rows.Select(r => new BodyPanelDto(
            r.Id, r.Code, r.DescriptionGr, r.DescriptionEn,
            r.Category, r.Side, r.DiagramX, r.DiagramY,
            r.Ops)).ToList();
    }
}
