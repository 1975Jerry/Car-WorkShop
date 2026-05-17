using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Customers;

public record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerDetailDto?>;

public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDetailDto?>
{
    private readonly IWorkshopDbContext _db;
    public GetCustomerByIdHandler(IWorkshopDbContext db) => _db = db;

    public async Task<CustomerDetailDto?> Handle(GetCustomerByIdQuery q, CancellationToken ct)
    {
        return await _db.Customers
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Id == q.Id)
            .Select(c => new CustomerDetailDto(
                c.Id,
                c.CustomerType,
                c.FirstName,
                c.LastName,
                c.CompanyName,
                c.VatNumber,
                c.TaxOffice,
                c.IdNumber,
                c.MobilePhone,
                c.SecondaryPhone,
                c.Email,
                c.AddressLine,
                c.City,
                c.PostalCode,
                c.GdprConsent,
                c.GdprConsentAt,
                c.Notes,
                c.IsActive,
                c.CreatedAt,
                c.UpdatedAt))
            .FirstOrDefaultAsync(ct);
    }
}
