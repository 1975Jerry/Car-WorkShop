using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Customers;

public record ListCustomersQuery(
    string? Search = null,
    bool? IsActive = null,
    CustomerType? CustomerType = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<CustomerListItemDto>>;

public class ListCustomersHandler : IRequestHandler<ListCustomersQuery, PagedList<CustomerListItemDto>>
{
    private readonly IWorkshopDbContext _db;
    public ListCustomersHandler(IWorkshopDbContext db) => _db = db;

    public async Task<PagedList<CustomerListItemDto>> Handle(ListCustomersQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var query = _db.Customers.AsNoTracking().Where(c => !c.IsDeleted);

        if (q.IsActive.HasValue)
            query = query.Where(c => c.IsActive == q.IsActive.Value);

        if (q.CustomerType.HasValue)
            query = query.Where(c => c.CustomerType == q.CustomerType.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c =>
                (c.FirstName != null && c.FirstName.ToLower().Contains(s)) ||
                (c.LastName != null && c.LastName.ToLower().Contains(s)) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)) ||
                c.MobilePhone.Contains(s) ||
                (c.SecondaryPhone != null && c.SecondaryPhone.Contains(s)) ||
                (c.VatNumber != null && c.VatNumber.Contains(s)) ||
                (c.TaxOffice != null && c.TaxOffice.ToLower().Contains(s)) ||
                (c.IdNumber != null && c.IdNumber.ToLower().Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)) ||
                (c.AddressLine != null && c.AddressLine.ToLower().Contains(s)) ||
                (c.City != null && c.City.ToLower().Contains(s)) ||
                (c.PostalCode != null && c.PostalCode.Contains(s)) ||
                (c.Notes != null && c.Notes.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(c => new CustomerListItemDto(
                c.Id,
                c.CustomerType,
                c.CustomerType == CustomerType.Company
                    ? (c.CompanyName ?? "—")
                    : (((c.LastName ?? "") + " " + (c.FirstName ?? "")).Trim()),
                c.VatNumber,
                c.MobilePhone,
                c.Email,
                c.City,
                c.Vehicles.Count(v => !v.IsDeleted),
                c.IsActive,
                c.CreatedAt))
            .ToListAsync(ct);

        return new PagedList<CustomerListItemDto>(items, page, size, total);
    }
}
