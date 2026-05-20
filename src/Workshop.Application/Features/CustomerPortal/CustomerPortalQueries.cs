using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.CustomerPortal;

/// <summary>
/// Document types a customer is allowed to view in the portal.
/// Internal-only types (CaseForm, InsuranceForm) are hidden.
/// </summary>
internal static class CustomerVisibleDocumentTypes
{
    public static readonly HashSet<DocumentType> Visible = new()
    {
        DocumentType.Quote,
        DocumentType.Invoice,
        DocumentType.Receipt,
        DocumentType.IdCopy,
        DocumentType.VehicleLicense,
        DocumentType.Other
    };
}

public record ListMyCasesQuery(Guid CustomerId) : IRequest<IReadOnlyList<MyCaseListItemDto>>;

public class ListMyCasesHandler(IWorkshopDbContext db) : IRequestHandler<ListMyCasesQuery, IReadOnlyList<MyCaseListItemDto>>
{
    public async Task<IReadOnlyList<MyCaseListItemDto>> Handle(ListMyCasesQuery q, CancellationToken ct)
    {
        var insurance = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.CustomerId == q.CustomerId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new MyCaseListItemDto(
                c.Id, PortalCaseKind.Insurance, c.CaseNumber,
                c.Status.ToString(),
                c.Vehicle.PlateNumber,
                c.Vehicle.Brand + " " + c.Vehicle.Model,
                c.Branch.Name,
                c.AccidentDate,
                c.UpdatedAt))
            .ToListAsync(ct);

        var retail = await db.RetailCases.AsNoTracking()
            .Where(c => c.CustomerId == q.CustomerId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new MyCaseListItemDto(
                c.Id, PortalCaseKind.Retail, c.CaseNumber,
                c.Status.ToString(),
                c.Vehicle.PlateNumber,
                c.Vehicle.Brand + " " + c.Vehicle.Model,
                c.Branch.Name,
                c.ScheduledDate,
                c.UpdatedAt))
            .ToListAsync(ct);

        return insurance.Concat(retail)
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }
}

public record GetMyInsuranceCaseQuery(Guid CustomerId, Guid CaseId) : IRequest<MyCaseDetailDto?>;

public class GetMyInsuranceCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetMyInsuranceCaseQuery, MyCaseDetailDto?>
{
    public async Task<MyCaseDetailDto?> Handle(GetMyInsuranceCaseQuery q, CancellationToken ct)
    {
        var c = await db.InsuranceCases.AsNoTracking()
            .Where(x => x.Id == q.CaseId && x.CustomerId == q.CustomerId)
            .Select(x => new
            {
                x.Id, x.CaseNumber, x.Status,
                x.Vehicle.PlateNumber, x.Vehicle.Brand, x.Vehicle.Model, x.Vehicle.Year,
                x.Branch.Name, x.Branch.AddressLine, x.Branch.Phone,
                InsurerName = x.InsuranceCompany.Name,
                x.ClaimNumber, x.AccidentDate, x.Notes,
                x.CreatedAt, x.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);
        if (c is null) return null;

        var approved = await db.InsuranceApprovals.AsNoTracking()
            .Where(a => a.InsuranceCaseId == q.CaseId)
            .Select(a => (decimal?)a.ApprovedAmount).FirstOrDefaultAsync(ct) ?? 0m;

        var paid = await db.Payments.AsNoTracking()
            .Where(p => p.InsuranceCaseId == q.CaseId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return new MyCaseDetailDto(
            c.Id, PortalCaseKind.Insurance, c.CaseNumber, c.Status.ToString(),
            c.PlateNumber, c.Brand, c.Model, c.Year,
            c.Name, c.AddressLine, c.Phone,
            c.InsurerName, c.ClaimNumber,
            WorkType: null,
            c.AccidentDate, ScheduledDate: null, c.Notes,
            approved, paid, Math.Max(0m, approved - paid), paid >= approved && approved > 0,
            c.CreatedAt, c.UpdatedAt);
    }
}

public record GetMyRetailCaseQuery(Guid CustomerId, Guid CaseId) : IRequest<MyCaseDetailDto?>;

public class GetMyRetailCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetMyRetailCaseQuery, MyCaseDetailDto?>
{
    public async Task<MyCaseDetailDto?> Handle(GetMyRetailCaseQuery q, CancellationToken ct)
    {
        var c = await db.RetailCases.AsNoTracking()
            .Where(x => x.Id == q.CaseId && x.CustomerId == q.CustomerId)
            .Select(x => new
            {
                x.Id, x.CaseNumber, x.Status,
                x.Vehicle.PlateNumber, x.Vehicle.Brand, x.Vehicle.Model, x.Vehicle.Year,
                x.Branch.Name, x.Branch.AddressLine, x.Branch.Phone,
                x.WorkType, x.TotalWithVat, x.ScheduledDate, x.Notes,
                x.CreatedAt, x.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);
        if (c is null) return null;

        var paid = await db.Payments.AsNoTracking()
            .Where(p => p.RetailCaseId == q.CaseId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return new MyCaseDetailDto(
            c.Id, PortalCaseKind.Retail, c.CaseNumber, c.Status.ToString(),
            c.PlateNumber, c.Brand, c.Model, c.Year,
            c.Name, c.AddressLine, c.Phone,
            InsuranceCompanyName: null, ClaimNumber: null,
            c.WorkType, AccidentDate: null, c.ScheduledDate, c.Notes,
            c.TotalWithVat, paid, Math.Max(0m, c.TotalWithVat - paid),
            paid >= c.TotalWithVat && c.TotalWithVat > 0,
            c.CreatedAt, c.UpdatedAt);
    }
}

public record GetMyCaseEventsQuery(Guid CustomerId, Guid CaseId, PortalCaseKind Kind)
    : IRequest<IReadOnlyList<MyCaseEventDto>>;

public class GetMyCaseEventsHandler(IWorkshopDbContext db)
    : IRequestHandler<GetMyCaseEventsQuery, IReadOnlyList<MyCaseEventDto>>
{
    public async Task<IReadOnlyList<MyCaseEventDto>> Handle(GetMyCaseEventsQuery q, CancellationToken ct)
    {
        var owns = q.Kind == PortalCaseKind.Insurance
            ? await db.InsuranceCases.AsNoTracking().AnyAsync(c => c.Id == q.CaseId && c.CustomerId == q.CustomerId, ct)
            : await db.RetailCases.AsNoTracking().AnyAsync(c => c.Id == q.CaseId && c.CustomerId == q.CustomerId, ct);
        if (!owns) return [];

        return await db.CaseEvents.AsNoTracking()
            .Where(e => q.Kind == PortalCaseKind.Insurance
                ? e.InsuranceCaseId == q.CaseId
                : e.RetailCaseId == q.CaseId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new MyCaseEventDto(e.ToStatus, e.OccurredAt))
            .ToListAsync(ct);
    }
}

public record GetMyCaseDocumentsQuery(Guid CustomerId, Guid CaseId, PortalCaseKind Kind)
    : IRequest<IReadOnlyList<MyDocumentDto>>;

public class GetMyCaseDocumentsHandler(IWorkshopDbContext db)
    : IRequestHandler<GetMyCaseDocumentsQuery, IReadOnlyList<MyDocumentDto>>
{
    public async Task<IReadOnlyList<MyDocumentDto>> Handle(GetMyCaseDocumentsQuery q, CancellationToken ct)
    {
        var owns = q.Kind == PortalCaseKind.Insurance
            ? await db.InsuranceCases.AsNoTracking().AnyAsync(c => c.Id == q.CaseId && c.CustomerId == q.CustomerId, ct)
            : await db.RetailCases.AsNoTracking().AnyAsync(c => c.Id == q.CaseId && c.CustomerId == q.CustomerId, ct);
        if (!owns) return [];

        var allowed = CustomerVisibleDocumentTypes.Visible;

        return await db.Documents.AsNoTracking()
            .Where(d => allowed.Contains(d.DocumentType) &&
                        (q.Kind == PortalCaseKind.Insurance
                            ? d.InsuranceCaseId == q.CaseId
                            : d.RetailCaseId == q.CaseId))
            .OrderBy(d => d.CreatedAt)
            .Select(d => new MyDocumentDto(
                d.Id, d.DocumentType, d.FileName, d.FilePath, d.SizeBytes, d.CreatedAt))
            .ToListAsync(ct);
    }
}

public record GetMyCasePhotosQuery(Guid CustomerId, Guid CaseId, PortalCaseKind Kind)
    : IRequest<IReadOnlyList<MyPhotoDto>>;

public class GetMyCasePhotosHandler(IWorkshopDbContext db)
    : IRequestHandler<GetMyCasePhotosQuery, IReadOnlyList<MyPhotoDto>>
{
    public async Task<IReadOnlyList<MyPhotoDto>> Handle(GetMyCasePhotosQuery q, CancellationToken ct)
    {
        if (q.Kind == PortalCaseKind.Insurance)
        {
            var owns = await db.InsuranceCases.AsNoTracking()
                .AnyAsync(c => c.Id == q.CaseId && c.CustomerId == q.CustomerId, ct);
            if (!owns) return [];

            return await db.Photos.AsNoTracking()
                .Where(p =>
                    (p.Assessment != null && p.Assessment.InsuranceCaseId == q.CaseId) ||
                    (p.Repair != null && p.Repair.InsuranceCaseId == q.CaseId))
                .OrderBy(p => p.CreatedAt)
                .Select(p => new MyPhotoDto(p.Id, p.FilePath, p.Phase.ToString(), p.CreatedAt))
                .ToListAsync(ct);
        }
        else
        {
            var owns = await db.RetailCases.AsNoTracking()
                .AnyAsync(c => c.Id == q.CaseId && c.CustomerId == q.CustomerId, ct);
            if (!owns) return [];

            return await db.Photos.AsNoTracking()
                .Where(p => p.RetailRepair != null && p.RetailRepair.RetailCaseId == q.CaseId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new MyPhotoDto(p.Id, p.FilePath, p.Phase.ToString(), p.CreatedAt))
                .ToListAsync(ct);
        }
    }
}
