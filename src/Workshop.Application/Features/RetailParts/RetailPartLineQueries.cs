using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.RetailParts;

public record GetPartLinesForRetailCaseQuery(Guid RetailCaseId) : IRequest<IReadOnlyList<RetailPartLineDto>>;

public class GetPartLinesForRetailCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetPartLinesForRetailCaseQuery, IReadOnlyList<RetailPartLineDto>>
{
    public async Task<IReadOnlyList<RetailPartLineDto>> Handle(GetPartLinesForRetailCaseQuery q, CancellationToken ct)
    {
        // Project FKs first, then look up names from separate dictionaries — required-nav joins on
        // InMemory silently drop rows (see feedback_inmemory_nav_joins).
        var raw = await db.RetailPartLines.AsNoTracking()
            .Where(p => p.RetailCaseId == q.RetailCaseId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.RetailCaseId,
                p.SupplierId,
                p.DestinationBranchId,
                p.PartType,
                p.PartName,
                p.Quantity,
                p.UnitCost,
                p.Total,
                p.ReceivedStatus,
                p.WarehouseId,
                p.StorageLocation,
                p.Notes,
                p.UpdatedAt
            })
            .ToListAsync(ct);

        if (raw.Count == 0) return [];

        var branchIds = raw.Select(r => r.DestinationBranchId).Distinct().ToList();
        var branches = await db.Branches.AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var supplierIds = raw.Where(r => r.SupplierId.HasValue).Select(r => r.SupplierId!.Value).Distinct().ToList();
        var suppliers = supplierIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Suppliers.AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var warehouseIds = raw.Where(r => r.WarehouseId.HasValue).Select(r => r.WarehouseId!.Value).Distinct().ToList();
        var warehouses = warehouseIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Warehouses.AsNoTracking()
                .Where(w => warehouseIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, w => w.Name, ct);

        return raw.Select(r => new RetailPartLineDto(
            r.Id,
            r.RetailCaseId,
            r.SupplierId,
            r.SupplierId.HasValue && suppliers.TryGetValue(r.SupplierId.Value, out var sn) ? sn : null,
            r.DestinationBranchId,
            branches.TryGetValue(r.DestinationBranchId, out var bn) ? bn : "—",
            r.PartType,
            r.PartName,
            r.Quantity,
            r.UnitCost,
            r.Total,
            r.ReceivedStatus,
            r.WarehouseId,
            r.WarehouseId.HasValue && warehouses.TryGetValue(r.WarehouseId.Value, out var wn) ? wn : null,
            r.StorageLocation,
            r.Notes,
            r.UpdatedAt)).ToList();
    }
}
