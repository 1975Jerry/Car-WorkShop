using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Vehicles;

public record ListVehiclesQuery(
    string? Search = null,
    Guid? CustomerId = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<VehicleListItemDto>>;

public class ListVehiclesHandler : IRequestHandler<ListVehiclesQuery, PagedList<VehicleListItemDto>>
{
    private readonly IWorkshopDbContext _db;
    public ListVehiclesHandler(IWorkshopDbContext db) => _db = db;

    public async Task<PagedList<VehicleListItemDto>> Handle(ListVehiclesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var query = _db.Vehicles.AsNoTracking().Where(v => !v.IsDeleted);

        if (q.CustomerId.HasValue)
            query = query.Where(v => v.CustomerId == q.CustomerId.Value);

        if (q.IsActive.HasValue)
            query = query.Where(v => v.IsActive == q.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(v =>
                v.PlateNumber.ToLower().Contains(s) ||
                (v.Vin != null && v.Vin.ToLower().Contains(s)) ||
                v.Brand.ToLower().Contains(s) ||
                v.Model.ToLower().Contains(s) ||
                (v.Version != null && v.Version.ToLower().Contains(s)) ||
                (v.Color != null && v.Color.ToLower().Contains(s)) ||
                (v.PolicyNumber != null && v.PolicyNumber.ToLower().Contains(s)) ||
                (v.Notes != null && v.Notes.ToLower().Contains(s)) ||
                (v.Customer.LastName != null && v.Customer.LastName.ToLower().Contains(s)) ||
                (v.Customer.FirstName != null && v.Customer.FirstName.ToLower().Contains(s)) ||
                (v.Customer.CompanyName != null && v.Customer.CompanyName.ToLower().Contains(s)) ||
                v.Customer.MobilePhone.Contains(s));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(v => v.UpdatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(v => new VehicleListItemDto(
                v.Id,
                v.PlateNumber,
                v.Brand,
                v.Model,
                v.Year,
                v.Color,
                v.Customer.CustomerType == CustomerType.Company
                    ? (v.Customer.CompanyName ?? "—")
                    : (((v.Customer.LastName ?? "") + " " + (v.Customer.FirstName ?? "")).Trim()),
                v.CustomerId,
                v.InsuranceCompany != null ? v.InsuranceCompany.Name : null,
                v.IsActive,
                v.CreatedAt))
            .ToListAsync(ct);

        return new PagedList<VehicleListItemDto>(items, page, size, total);
    }
}

public record GetVehicleByIdQuery(Guid Id) : IRequest<VehicleDetailDto?>;

public class GetVehicleByIdHandler : IRequestHandler<GetVehicleByIdQuery, VehicleDetailDto?>
{
    private readonly IWorkshopDbContext _db;
    public GetVehicleByIdHandler(IWorkshopDbContext db) => _db = db;

    public async Task<VehicleDetailDto?> Handle(GetVehicleByIdQuery q, CancellationToken ct)
    {
        return await _db.Vehicles
            .AsNoTracking()
            .Where(v => !v.IsDeleted && v.Id == q.Id)
            .Select(v => new VehicleDetailDto(
                v.Id, v.CustomerId, v.PlateNumber, v.Vin, v.Brand, v.Model, v.Version,
                v.Year, v.Color, v.FuelType, v.Mileage, v.InsuranceCompanyId,
                v.PolicyNumber, v.InsuranceExpiration, v.Notes, v.IsActive))
            .FirstOrDefaultAsync(ct);
    }
}
