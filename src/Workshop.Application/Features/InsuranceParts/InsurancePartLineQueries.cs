using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.InsuranceParts;

public record GetPartLinesForCaseQuery(Guid InsuranceCaseId) : IRequest<IReadOnlyList<InsurancePartLineDto>>;

public class GetPartLinesForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetPartLinesForCaseQuery, IReadOnlyList<InsurancePartLineDto>>
{
    public async Task<IReadOnlyList<InsurancePartLineDto>> Handle(GetPartLinesForCaseQuery q, CancellationToken ct)
    {
        return await db.InsurancePartLines.AsNoTracking()
            .Where(p => p.Assessment.InsuranceCaseId == q.InsuranceCaseId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new InsurancePartLineDto(
                p.Id,
                p.AssessmentId,
                p.SupplierId,
                p.Supplier != null ? p.Supplier.Name : null,
                p.DestinationBranchId,
                p.DestinationBranch.Name,
                p.PartType,
                p.PartName,
                p.Quantity,
                p.UnitCost,
                p.DiscountPct,
                p.Total,
                p.AvailabilityStatus,
                p.InsuranceApproved,
                p.Ordered,
                p.OrderDate,
                p.ReceivedStatus,
                p.ReceivedDate,
                p.WarehouseId,
                p.Warehouse != null ? p.Warehouse.Name : null,
                p.StorageLocation,
                p.Notes,
                p.UpdatedAt))
            .ToListAsync(ct);
    }
}
