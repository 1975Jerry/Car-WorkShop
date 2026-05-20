using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.SupplierPortal;

public record ListSupplierOrdersQuery(
    Guid SupplierId,
    PartReceivedStatus? StatusFilter = null) : IRequest<IReadOnlyList<SupplierOrderRow>>;

public class ListSupplierOrdersHandler(IWorkshopDbContext db)
    : IRequestHandler<ListSupplierOrdersQuery, IReadOnlyList<SupplierOrderRow>>
{
    public async Task<IReadOnlyList<SupplierOrderRow>> Handle(ListSupplierOrdersQuery q, CancellationToken ct)
    {
        var insurance = await db.InsurancePartLines.AsNoTracking()
            .Where(p => p.SupplierId == q.SupplierId
                        && (q.StatusFilter == null || p.ReceivedStatus == q.StatusFilter))
            .Select(p => new
            {
                p.Id,
                CaseNumber = p.Assessment.InsuranceCase.CaseNumber,
                p.PartName,
                p.PartType,
                p.Quantity,
                p.UnitCost,
                p.Total,
                p.ReceivedStatus,
                p.DestinationBranchId,
                p.OrderDate,
                p.Notes,
                p.UpdatedAt
            })
            .ToListAsync(ct);

        var retail = await db.RetailPartLines.AsNoTracking()
            .Where(p => p.SupplierId == q.SupplierId
                        && (q.StatusFilter == null || p.ReceivedStatus == q.StatusFilter))
            .Select(p => new
            {
                p.Id,
                CaseNumber = p.RetailCase.CaseNumber,
                p.PartName,
                p.PartType,
                p.Quantity,
                p.UnitCost,
                p.Total,
                p.ReceivedStatus,
                p.DestinationBranchId,
                OrderDate = (DateOnly?)null,
                p.Notes,
                p.UpdatedAt
            })
            .ToListAsync(ct);

        // Resolve destination branch names from a single lookup dictionary.
        var branchIds = insurance.Select(x => x.DestinationBranchId)
            .Concat(retail.Select(x => x.DestinationBranchId))
            .Distinct().ToList();
        var branches = branchIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Branches.AsNoTracking()
                .Where(b => branchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var rows = new List<SupplierOrderRow>(insurance.Count + retail.Count);
        rows.AddRange(insurance.Select(x => new SupplierOrderRow(
            x.Id, SupplierLineKind.Insurance, x.CaseNumber, x.PartName, x.PartType,
            x.Quantity, x.UnitCost, x.Total, x.ReceivedStatus,
            branches.GetValueOrDefault(x.DestinationBranchId, "—"),
            x.OrderDate, x.Notes, x.UpdatedAt)));
        rows.AddRange(retail.Select(x => new SupplierOrderRow(
            x.Id, SupplierLineKind.Retail, x.CaseNumber, x.PartName, x.PartType,
            x.Quantity, x.UnitCost, x.Total, x.ReceivedStatus,
            branches.GetValueOrDefault(x.DestinationBranchId, "—"),
            x.OrderDate, x.Notes, x.UpdatedAt)));

        return rows.OrderByDescending(r => r.UpdatedAt).ToList();
    }
}
