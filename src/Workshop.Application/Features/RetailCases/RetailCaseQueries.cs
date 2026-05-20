using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.RetailCases;

public record ListRetailCasesQuery(
    string? Search = null,
    RetailCaseStatus? Status = null,
    Guid? BranchId = null,
    Guid? AssignedUserId = null,
    Guid? CustomerId = null,
    Guid? VehicleId = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<RetailCaseListItemDto>>;

public class ListRetailCasesHandler(IWorkshopDbContext db)
    : IRequestHandler<ListRetailCasesQuery, PagedList<RetailCaseListItemDto>>
{
    public async Task<PagedList<RetailCaseListItemDto>> Handle(ListRetailCasesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var query = db.RetailCases.AsNoTracking();

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
                c.WorkType.ToLower().Contains(s) ||
                (c.Notes != null && c.Notes.ToLower().Contains(s)) ||
                c.Vehicle.PlateNumber.ToLower().Contains(s) ||
                c.Vehicle.Brand.ToLower().Contains(s) ||
                c.Vehicle.Model.ToLower().Contains(s) ||
                (c.Customer.LastName != null && c.Customer.LastName.ToLower().Contains(s)) ||
                (c.Customer.FirstName != null && c.Customer.FirstName.ToLower().Contains(s)) ||
                (c.Customer.CompanyName != null && c.Customer.CompanyName.ToLower().Contains(s)) ||
                c.Customer.MobilePhone.Contains(s) ||
                c.Branch.Name.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(c => new RetailCaseListItemDto(
                c.Id,
                c.CaseNumber,
                c.Status,
                c.Customer.CustomerType == CustomerType.Company
                    ? (c.Customer.CompanyName ?? "—")
                    : (((c.Customer.LastName ?? "") + " " + (c.Customer.FirstName ?? "")).Trim()),
                c.Vehicle.PlateNumber,
                c.Vehicle.Brand + " " + c.Vehicle.Model,
                c.Branch.Name,
                c.AssignedUser != null ? c.AssignedUser.FullName : null,
                c.WorkType,
                c.TotalWithVat,
                c.ScheduledDate,
                c.CompletedAt,
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(ct);

        return new PagedList<RetailCaseListItemDto>(items, page, size, total);
    }
}

public record GetRetailCaseByIdQuery(Guid Id) : IRequest<RetailCaseDetailDto?>;

public class GetRetailCaseByIdHandler(IWorkshopDbContext db)
    : IRequestHandler<GetRetailCaseByIdQuery, RetailCaseDetailDto?>
{
    public async Task<RetailCaseDetailDto?> Handle(GetRetailCaseByIdQuery q, CancellationToken ct) =>
        await db.RetailCases.AsNoTracking().Where(c => c.Id == q.Id)
            .Select(c => new RetailCaseDetailDto(
                c.Id, c.CaseNumber, c.Status,
                c.CustomerId,
                c.Customer.CustomerType == CustomerType.Company
                    ? (c.Customer.CompanyName ?? "—")
                    : (((c.Customer.LastName ?? "") + " " + (c.Customer.FirstName ?? "")).Trim()),
                c.Customer.MobilePhone,
                c.Customer.Email,
                c.VehicleId, c.Vehicle.PlateNumber, c.Vehicle.Brand, c.Vehicle.Model,
                c.Vehicle.Year, c.Vehicle.Color,
                c.BranchId, c.Branch.Name,
                c.AssignedUserId, c.AssignedUser != null ? c.AssignedUser.FullName : null,
                c.WorkType, c.FinalCost, c.VatAmount, c.TotalWithVat,
                c.ScheduledDate, c.CompletedAt, c.Notes,
                c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(ct);
}

public record GetRetailCaseEventsQuery(Guid CaseId) : IRequest<IReadOnlyList<RetailCaseEventDto>>;

public class GetRetailCaseEventsHandler(IWorkshopDbContext db)
    : IRequestHandler<GetRetailCaseEventsQuery, IReadOnlyList<RetailCaseEventDto>>
{
    public async Task<IReadOnlyList<RetailCaseEventDto>> Handle(GetRetailCaseEventsQuery q, CancellationToken ct)
    {
        // Project FK first then look up user names (see feedback_inmemory_nav_joins).
        var raw = await db.CaseEvents.AsNoTracking()
            .Where(e => e.RetailCaseId == q.CaseId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new
            {
                e.Id,
                e.FromStatus,
                e.ToStatus,
                e.TriggeredById,
                e.Reason,
                e.OccurredAt
            })
            .ToListAsync(ct);

        var userIds = raw.Select(r => r.TriggeredById).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return raw.Select(r => new RetailCaseEventDto(
            r.Id, r.FromStatus, r.ToStatus,
            users.TryGetValue(r.TriggeredById, out var name) ? name : "—",
            r.Reason, r.OccurredAt)).ToList();
    }
}
