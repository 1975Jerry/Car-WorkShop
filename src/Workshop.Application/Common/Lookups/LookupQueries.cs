using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Common.Lookups;

public record LookupItem(Guid Id, string Label);

public record GetCustomerLookupQuery(string? Search = null, int Take = 50) : IRequest<IReadOnlyList<LookupItem>>;

public class GetCustomerLookupHandler : IRequestHandler<GetCustomerLookupQuery, IReadOnlyList<LookupItem>>
{
    private readonly IWorkshopDbContext _db;
    public GetCustomerLookupHandler(IWorkshopDbContext db) => _db = db;

    public async Task<IReadOnlyList<LookupItem>> Handle(GetCustomerLookupQuery q, CancellationToken ct)
    {
        var query = _db.Customers.AsNoTracking().Where(c => !c.IsDeleted && c.IsActive);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c =>
                (c.FirstName != null && c.FirstName.ToLower().Contains(s)) ||
                (c.LastName != null && c.LastName.ToLower().Contains(s)) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)) ||
                c.MobilePhone.Contains(s));
        }

        return await query
            .OrderBy(c => c.LastName).ThenBy(c => c.CompanyName)
            .Take(q.Take)
            .Select(c => new LookupItem(
                c.Id,
                c.CustomerType == CustomerType.Company
                    ? (c.CompanyName ?? "—")
                    : (((c.LastName ?? "") + " " + (c.FirstName ?? "")).Trim())))
            .ToListAsync(ct);
    }
}

public record GetInsuranceCompanyLookupQuery : IRequest<IReadOnlyList<LookupItem>>;

public class GetInsuranceCompanyLookupHandler
    : IRequestHandler<GetInsuranceCompanyLookupQuery, IReadOnlyList<LookupItem>>
{
    private readonly IWorkshopDbContext _db;
    public GetInsuranceCompanyLookupHandler(IWorkshopDbContext db) => _db = db;

    public async Task<IReadOnlyList<LookupItem>> Handle(GetInsuranceCompanyLookupQuery _, CancellationToken ct)
    {
        return await _db.InsuranceCompanies
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new LookupItem(c.Id, c.Name))
            .ToListAsync(ct);
    }
}

public record GetBranchLookupQuery : IRequest<IReadOnlyList<LookupItem>>;

public class GetBranchLookupHandler : IRequestHandler<GetBranchLookupQuery, IReadOnlyList<LookupItem>>
{
    private readonly IWorkshopDbContext _db;
    public GetBranchLookupHandler(IWorkshopDbContext db) => _db = db;

    public async Task<IReadOnlyList<LookupItem>> Handle(GetBranchLookupQuery _, CancellationToken ct)
    {
        return await _db.Branches
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new LookupItem(b.Id, b.Name))
            .ToListAsync(ct);
    }
}

public record GetWarehouseLookupQuery(Guid? BranchId = null) : IRequest<IReadOnlyList<LookupItem>>;

public class GetWarehouseLookupHandler(IWorkshopDbContext db)
    : IRequestHandler<GetWarehouseLookupQuery, IReadOnlyList<LookupItem>>
{
    public async Task<IReadOnlyList<LookupItem>> Handle(GetWarehouseLookupQuery q, CancellationToken ct)
    {
        var query = db.Warehouses.AsNoTracking().Where(w => !w.IsDeleted);
        if (q.BranchId.HasValue) query = query.Where(w => w.BranchId == q.BranchId.Value);
        return await query
            .OrderBy(w => w.Branch.Name)
            .Select(w => new LookupItem(w.Id, w.Branch.Name + " — " + w.Name))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Active staff users — used today for technician assignment on a Repair. The full
/// "users currently in the Technician role" filter belongs in Identity, but the
/// schema doesn't expose AspNetUserRoles cleanly from Application; for now the
/// lookup returns all active staff and we let the manager pick. Tighten in Phase 10
/// when the staff-role admin surface lands.
/// </summary>
public record GetStaffUserLookupQuery(Guid? BranchId = null) : IRequest<IReadOnlyList<LookupItem>>;

public class GetStaffUserLookupHandler(IWorkshopDbContext db)
    : IRequestHandler<GetStaffUserLookupQuery, IReadOnlyList<LookupItem>>
{
    public async Task<IReadOnlyList<LookupItem>> Handle(GetStaffUserLookupQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking()
            .Where(u => u.PortalAudience == Workshop.Domain.Enums.PortalAudience.Staff && u.IsActive);
        if (q.BranchId.HasValue)
            query = query.Where(u => u.BranchId == q.BranchId.Value);
        return await query
            .OrderBy(u => u.FullName)
            .Select(u => new LookupItem(u.Id, u.FullName))
            .ToListAsync(ct);
    }
}

public record GetSupplierLookupQuery(string? Search = null, int Take = 50) : IRequest<IReadOnlyList<LookupItem>>;

public class GetSupplierLookupHandler(IWorkshopDbContext db)
    : IRequestHandler<GetSupplierLookupQuery, IReadOnlyList<LookupItem>>
{
    public async Task<IReadOnlyList<LookupItem>> Handle(GetSupplierLookupQuery q, CancellationToken ct)
    {
        var query = db.Suppliers.AsNoTracking().Where(s => !s.IsDeleted && s.IsActive);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(s));
        }
        return await query
            .OrderBy(x => x.Name)
            .Take(q.Take)
            .Select(x => new LookupItem(x.Id, x.Name))
            .ToListAsync(ct);
    }
}
