using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsuranceCases;

public record ListInsuranceCasesQuery(
    string? Search = null,
    InsuranceCaseStatus? Status = null,
    Guid? BranchId = null,
    Guid? AssignedUserId = null,
    Guid? CustomerId = null,
    Guid? VehicleId = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<InsuranceCaseListItemDto>>;

public class ListInsuranceCasesHandler(IWorkshopDbContext db)
    : IRequestHandler<ListInsuranceCasesQuery, PagedList<InsuranceCaseListItemDto>>
{
    public async Task<PagedList<InsuranceCaseListItemDto>> Handle(ListInsuranceCasesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var query = db.InsuranceCases.AsNoTracking();

        if (q.Status.HasValue) query = query.Where(c => c.Status == q.Status.Value);
        if (q.BranchId.HasValue) query = query.Where(c => c.BranchId == q.BranchId.Value);
        if (q.AssignedUserId.HasValue) query = query.Where(c => c.AssignedUserId == q.AssignedUserId.Value);
        if (q.CustomerId.HasValue) query = query.Where(c => c.CustomerId == q.CustomerId.Value);
        if (q.VehicleId.HasValue) query = query.Where(c => c.VehicleId == q.VehicleId.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c =>
                c.CaseNumber.ToLower().Contains(s) ||
                (c.ClaimNumber != null && c.ClaimNumber.ToLower().Contains(s)) ||
                c.Vehicle.PlateNumber.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(c => new InsuranceCaseListItemDto(
                c.Id,
                c.CaseNumber,
                c.Status,
                c.Priority,
                c.Customer.CustomerType == CustomerType.Company
                    ? (c.Customer.CompanyName ?? "—")
                    : (((c.Customer.LastName ?? "") + " " + (c.Customer.FirstName ?? "")).Trim()),
                c.Vehicle.PlateNumber,
                c.Vehicle.Brand + " " + c.Vehicle.Model,
                c.InsuranceCompany.Name,
                c.Branch.Name,
                c.AssignedUser != null ? c.AssignedUser.FullName : null,
                c.AccidentDate,
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(ct);

        return new PagedList<InsuranceCaseListItemDto>(items, page, size, total);
    }
}

public record GetInsuranceCaseByIdQuery(Guid Id) : IRequest<InsuranceCaseDetailDto?>;

public class GetInsuranceCaseByIdHandler(IWorkshopDbContext db)
    : IRequestHandler<GetInsuranceCaseByIdQuery, InsuranceCaseDetailDto?>
{
    public async Task<InsuranceCaseDetailDto?> Handle(GetInsuranceCaseByIdQuery q, CancellationToken ct) =>
        await db.InsuranceCases.AsNoTracking().Where(c => c.Id == q.Id)
            .Select(c => new InsuranceCaseDetailDto(
                c.Id, c.CaseNumber, c.Status, c.Priority,
                c.CustomerId,
                c.Customer.CustomerType == CustomerType.Company
                    ? (c.Customer.CompanyName ?? "—")
                    : (((c.Customer.LastName ?? "") + " " + (c.Customer.FirstName ?? "")).Trim()),
                c.Customer.MobilePhone,
                c.Customer.Email,
                c.VehicleId, c.Vehicle.PlateNumber, c.Vehicle.Brand, c.Vehicle.Model,
                c.Vehicle.Year, c.Vehicle.Color,
                c.BranchId, c.Branch.Name,
                c.InsuranceCompanyId, c.InsuranceCompany.Name, c.ClaimNumber,
                c.AssessorId, c.Assessor != null ? c.Assessor.FullName : null, c.Assessor != null ? c.Assessor.Phone : null,
                c.AdjusterId, c.Adjuster != null ? c.Adjuster.FullName : null, c.Adjuster != null ? c.Adjuster.Phone : null,
                c.AssignedUserId, c.AssignedUser != null ? c.AssignedUser.FullName : null,
                c.DriverFirstName, c.DriverLastName, c.DriverPhone, c.DriverEmail,
                c.AccidentDate, c.MileageAtAssessment, c.ClosedAt, c.Notes,
                c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(ct);
}

public record GetCaseEventsQuery(Guid CaseId) : IRequest<IReadOnlyList<CaseEventDto>>;

public class GetCaseEventsHandler(IWorkshopDbContext db)
    : IRequestHandler<GetCaseEventsQuery, IReadOnlyList<CaseEventDto>>
{
    public async Task<IReadOnlyList<CaseEventDto>> Handle(GetCaseEventsQuery q, CancellationToken ct) =>
        await db.CaseEvents.AsNoTracking()
            .Where(e => e.InsuranceCaseId == q.CaseId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new CaseEventDto(
                e.Id, e.FromStatus, e.ToStatus,
                e.TriggeredBy.FullName,
                e.Reason, e.OccurredAt))
            .ToListAsync(ct);
}
