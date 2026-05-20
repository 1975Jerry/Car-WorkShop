using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Payments;

public record GetPaymentsForCaseQuery(Guid InsuranceCaseId) : IRequest<IReadOnlyList<PaymentDto>>;

public class GetPaymentsForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetPaymentsForCaseQuery, IReadOnlyList<PaymentDto>>
{
    public async Task<IReadOnlyList<PaymentDto>> Handle(GetPaymentsForCaseQuery q, CancellationToken ct) =>
        await db.Payments.AsNoTracking()
            .Where(p => p.InsuranceCaseId == q.InsuranceCaseId)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new PaymentDto(
                p.Id,
                p.InsuranceCaseId!.Value,
                p.Amount,
                p.PaymentDate,
                p.PaymentMethod,
                p.Payer,
                p.ReferenceNumber,
                p.Notes,
                p.CreatedAt))
            .ToListAsync(ct);
}

public record GetPaymentsForRetailCaseQuery(Guid RetailCaseId) : IRequest<IReadOnlyList<PaymentDto>>;

public class GetPaymentsForRetailCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetPaymentsForRetailCaseQuery, IReadOnlyList<PaymentDto>>
{
    public async Task<IReadOnlyList<PaymentDto>> Handle(GetPaymentsForRetailCaseQuery q, CancellationToken ct) =>
        await db.Payments.AsNoTracking()
            .Where(p => p.RetailCaseId == q.RetailCaseId)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new PaymentDto(
                p.Id,
                p.RetailCaseId!.Value,
                p.Amount,
                p.PaymentDate,
                p.PaymentMethod,
                p.Payer,
                p.ReferenceNumber,
                p.Notes,
                p.CreatedAt))
            .ToListAsync(ct);
}

public record GetRetailSettlementSummaryQuery(Guid RetailCaseId) : IRequest<CaseSettlementSummaryDto>;

public class GetRetailSettlementSummaryHandler(IWorkshopDbContext db)
    : IRequestHandler<GetRetailSettlementSummaryQuery, CaseSettlementSummaryDto>
{
    public async Task<CaseSettlementSummaryDto> Handle(GetRetailSettlementSummaryQuery q, CancellationToken ct)
    {
        var caseId = q.RetailCaseId;
        var agreed = await db.RetailCases.AsNoTracking()
            .Where(c => c.Id == caseId)
            .Select(c => (decimal?)c.TotalWithVat).FirstOrDefaultAsync(ct) ?? 0m;

        var paid = await db.Payments.AsNoTracking()
            .Where(p => p.RetailCaseId == caseId)
            .Select(p => (decimal?)p.Amount)
            .ToListAsync(ct);
        var totalPaid = paid.Sum(x => x ?? 0m);

        return new CaseSettlementSummaryDto(
            agreed,
            totalPaid,
            Math.Max(0m, agreed - totalPaid),
            totalPaid >= agreed && agreed > 0,
            // Retail flow doesn't track an insurance-sent doc count; surface 0.
            0);
    }
}

public record GetCaseSettlementSummaryQuery(Guid InsuranceCaseId) : IRequest<CaseSettlementSummaryDto>;

public class GetCaseSettlementSummaryHandler(IWorkshopDbContext db)
    : IRequestHandler<GetCaseSettlementSummaryQuery, CaseSettlementSummaryDto>
{
    public async Task<CaseSettlementSummaryDto> Handle(GetCaseSettlementSummaryQuery q, CancellationToken ct)
    {
        var caseId = q.InsuranceCaseId;
        var agreed = await db.InsuranceApprovals.AsNoTracking()
            .Where(a => a.InsuranceCaseId == caseId)
            .Select(a => (decimal?)a.ApprovedAmount).FirstOrDefaultAsync(ct) ?? 0m;

        var paid = await db.Payments.AsNoTracking()
            .Where(p => p.InsuranceCaseId == caseId)
            .Select(p => (decimal?)p.Amount)
            .ToListAsync(ct);
        var totalPaid = paid.Sum(x => x ?? 0m);

        var sentCount = await db.Documents.AsNoTracking()
            .Where(d => d.InsuranceCaseId == caseId && d.SentToInsurance)
            .CountAsync(ct);

        return new CaseSettlementSummaryDto(
            agreed,
            totalPaid,
            Math.Max(0m, agreed - totalPaid),
            totalPaid >= agreed && agreed > 0,
            sentCount);
    }
}
