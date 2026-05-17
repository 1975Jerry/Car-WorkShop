using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsurerPortal;

public record ListInsurerCasesQuery(
    Guid InsuranceCompanyId,
    ApprovalStatus? ApprovalFilter = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<InsurerCaseListItemDto>>;

public class ListInsurerCasesHandler(IWorkshopDbContext db)
    : IRequestHandler<ListInsurerCasesQuery, PagedList<InsurerCaseListItemDto>>
{
    public async Task<PagedList<InsurerCaseListItemDto>> Handle(ListInsurerCasesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var query = db.InsuranceCases.AsNoTracking()
            .Where(c => c.InsuranceCompanyId == q.InsuranceCompanyId);

        // Filter on approval status (joined from InsuranceApprovals).
        if (q.ApprovalFilter is { } targetStatus)
        {
            query = query.Where(c =>
                db.InsuranceApprovals.Any(a => a.InsuranceCaseId == c.Id && a.ApprovalStatus == targetStatus));
        }

        var total = await query.CountAsync(ct);

        var raw = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(c => new
            {
                c.Id, c.CaseNumber, c.Status, c.ClaimNumber, c.AccidentDate, c.UpdatedAt,
                c.Vehicle.PlateNumber,
                VehicleBrandModel = c.Vehicle.Brand + " " + c.Vehicle.Model,
                CustomerDisplayName = c.Customer.CustomerType == CustomerType.Company
                    ? (c.Customer.CompanyName ?? "—")
                    : (((c.Customer.LastName ?? "") + " " + (c.Customer.FirstName ?? "")).Trim())
            })
            .ToListAsync(ct);

        if (raw.Count == 0)
            return new PagedList<InsurerCaseListItemDto>([], page, size, total);

        var caseIds = raw.Select(r => r.Id).ToList();
        var approvals = await db.InsuranceApprovals.AsNoTracking()
            .Where(a => caseIds.Contains(a.InsuranceCaseId))
            .ToDictionaryAsync(a => a.InsuranceCaseId,
                a => new { a.ApprovalStatus, a.ApprovedAmount }, ct);

        var items = raw.Select(r =>
        {
            approvals.TryGetValue(r.Id, out var ap);
            return new InsurerCaseListItemDto(
                r.Id, r.CaseNumber, r.Status,
                ap?.ApprovalStatus, ap?.ApprovedAmount,
                r.PlateNumber, r.VehicleBrandModel,
                r.ClaimNumber, r.CustomerDisplayName, r.AccidentDate, r.UpdatedAt);
        }).ToList();

        return new PagedList<InsurerCaseListItemDto>(items, page, size, total);
    }
}

public record GetInsurerCaseDetailQuery(Guid InsuranceCompanyId, Guid CaseId)
    : IRequest<InsurerCaseDetailDto?>;

public class GetInsurerCaseDetailHandler(IWorkshopDbContext db)
    : IRequestHandler<GetInsurerCaseDetailQuery, InsurerCaseDetailDto?>
{
    public async Task<InsurerCaseDetailDto?> Handle(GetInsurerCaseDetailQuery q, CancellationToken ct)
    {
        var c = await db.InsuranceCases.AsNoTracking()
            .Where(x => x.Id == q.CaseId && x.InsuranceCompanyId == q.InsuranceCompanyId)
            .Select(x => new
            {
                x.Id, x.CaseNumber, x.Status,
                x.Vehicle.PlateNumber, x.Vehicle.Brand, x.Vehicle.Model, x.Vehicle.Year, x.Vehicle.Color,
                CustomerDisplayName = x.Customer.CustomerType == CustomerType.Company
                    ? (x.Customer.CompanyName ?? "—")
                    : (((x.Customer.LastName ?? "") + " " + (x.Customer.FirstName ?? "")).Trim()),
                x.Customer.MobilePhone, x.Customer.VatNumber,
                x.ClaimNumber, x.AccidentDate, x.MileageAtAssessment,
                x.DriverFirstName, x.DriverLastName, x.DriverPhone
            })
            .FirstOrDefaultAsync(ct);
        if (c is null) return null;

        var quote = await db.Quotes.AsNoTracking()
            .Where(qz => qz.InsuranceCaseId == q.CaseId)
            .OrderByDescending(qz => qz.IssueDate)
            .Select(qz => new InsurerQuoteSummary(
                qz.Id, qz.QuoteNumber, qz.IssueDate,
                qz.LaborSubtotal, qz.PartsSubtotal, qz.Subtotal,
                qz.VatRate, qz.VatAmount, qz.Total, qz.PdfPath))
            .FirstOrDefaultAsync(ct);

        var approval = await db.InsuranceApprovals.AsNoTracking()
            .Where(a => a.InsuranceCaseId == q.CaseId)
            .Select(a => new InsurerApprovalSummary(
                a.Id, a.LiabilityAccepted, a.CustomerParticipation,
                a.ParticipationAmount, a.ApprovedAmount, a.ApprovalDate,
                a.ApprovalStatus, a.Notes, a.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        // Project FK first, then lookup panel codes separately (InMemory nav-join pitfall).
        var workItemRaw = await db.WorkItems.AsNoTracking()
            .Where(w => w.Assessment.InsuranceCaseId == q.CaseId)
            .Select(w => new { w.BodyPanelId, w.Description, w.Total })
            .ToListAsync(ct);
        var panelIds = workItemRaw.Where(w => w.BodyPanelId.HasValue)
            .Select(w => w.BodyPanelId!.Value).Distinct().ToList();
        var panels = panelIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.BodyPanels.AsNoTracking()
                .Where(p => panelIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var workItems = workItemRaw.Select(w => new InsurerWorkItemRow(
            w.BodyPanelId.HasValue && panels.TryGetValue(w.BodyPanelId.Value, out var code) ? code : null,
            w.Description, w.Total)).ToList();

        var parts = await db.InsurancePartLines.AsNoTracking()
            .Where(p => p.Assessment.InsuranceCaseId == q.CaseId
                        && p.ReceivedStatus != PartReceivedStatus.Cancelled)
            .Select(p => new InsurerPartRow(
                p.PartName, p.PartType, p.Quantity, p.UnitCost, p.Total))
            .ToListAsync(ct);

        return new InsurerCaseDetailDto(
            c.Id, c.CaseNumber, c.Status,
            c.PlateNumber, c.Brand, c.Model, c.Year, c.Color,
            c.CustomerDisplayName, c.MobilePhone, c.VatNumber,
            c.ClaimNumber, c.AccidentDate, c.MileageAtAssessment,
            c.DriverFirstName, c.DriverLastName, c.DriverPhone,
            quote, approval, workItems, parts);
    }
}

public record GetInsurerSentDocumentsQuery(Guid InsuranceCompanyId, Guid CaseId)
    : IRequest<IReadOnlyList<InsurerSentDocumentDto>>;

public class GetInsurerSentDocumentsHandler(IWorkshopDbContext db)
    : IRequestHandler<GetInsurerSentDocumentsQuery, IReadOnlyList<InsurerSentDocumentDto>>
{
    public async Task<IReadOnlyList<InsurerSentDocumentDto>> Handle(
        GetInsurerSentDocumentsQuery q, CancellationToken ct)
    {
        var owns = await db.InsuranceCases.AsNoTracking()
            .AnyAsync(c => c.Id == q.CaseId && c.InsuranceCompanyId == q.InsuranceCompanyId, ct);
        if (!owns) return [];

        return await db.Documents.AsNoTracking()
            .Where(d => d.InsuranceCaseId == q.CaseId && d.SentToInsurance)
            .OrderBy(d => d.SentToInsuranceAt)
            .Select(d => new InsurerSentDocumentDto(
                d.Id, d.DocumentType, d.FileName, d.FilePath, d.SizeBytes,
                d.SentToInsuranceAt ?? d.CreatedAt))
            .ToListAsync(ct);
    }
}
