using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Application.Features.Suppliers;

public record SupplierListItemDto(Guid Id, string Name, string? VatNumber, string? Phone,
    string? Email, string? ContactPerson, bool IsActive);

public record SupplierDetailDto(Guid Id, string Name, string? VatNumber, string? Phone, string? Email,
    string? AddressLine, string? ContactPerson, string? Notes, bool IsActive);

public record SupplierUpsertDto(string Name, string? VatNumber, string? Phone, string? Email,
    string? AddressLine, string? ContactPerson, string? Notes, bool IsActive);

public record ListSuppliersQuery(string? Search = null, bool? IsActive = null,
    int Page = 1, int PageSize = 25) : IRequest<PagedList<SupplierListItemDto>>;

public class ListSuppliersHandler(IWorkshopDbContext db)
    : IRequestHandler<ListSuppliersQuery, PagedList<SupplierListItemDto>>
{
    public async Task<PagedList<SupplierListItemDto>> Handle(ListSuppliersQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var query = db.Suppliers.AsNoTracking();
        if (q.IsActive.HasValue) query = query.Where(s => s.IsActive == q.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(s)
                || (x.VatNumber != null && x.VatNumber.Contains(s))
                || (x.ContactPerson != null && x.ContactPerson.ToLower().Contains(s)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new SupplierListItemDto(
                x.Id, x.Name, x.VatNumber, x.Phone, x.Email, x.ContactPerson, x.IsActive))
            .ToListAsync(ct);
        return new PagedList<SupplierListItemDto>(items, page, size, total);
    }
}

public record GetSupplierByIdQuery(Guid Id) : IRequest<SupplierDetailDto?>;

public class GetSupplierByIdHandler(IWorkshopDbContext db)
    : IRequestHandler<GetSupplierByIdQuery, SupplierDetailDto?>
{
    public async Task<SupplierDetailDto?> Handle(GetSupplierByIdQuery q, CancellationToken ct) =>
        await db.Suppliers.AsNoTracking().Where(s => s.Id == q.Id)
            .Select(s => new SupplierDetailDto(
                s.Id, s.Name, s.VatNumber, s.Phone, s.Email, s.AddressLine,
                s.ContactPerson, s.Notes, s.IsActive))
            .FirstOrDefaultAsync(ct);
}

public record CreateSupplierCommand(SupplierUpsertDto Data) : IRequest<Guid>;
public class CreateSupplierHandler(IWorkshopDbContext db) : IRequestHandler<CreateSupplierCommand, Guid>
{
    public async Task<Guid> Handle(CreateSupplierCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        var entity = new Supplier
        {
            Name = d.Name, VatNumber = d.VatNumber, Phone = d.Phone, Email = d.Email,
            AddressLine = d.AddressLine, ContactPerson = d.ContactPerson,
            Notes = d.Notes, IsActive = d.IsActive
        };
        db.Suppliers.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateSupplierCommand(Guid Id, SupplierUpsertDto Data) : IRequest;
public class UpdateSupplierHandler(IWorkshopDbContext db) : IRequestHandler<UpdateSupplierCommand>
{
    public async Task Handle(UpdateSupplierCommand cmd, CancellationToken ct)
    {
        var entity = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Supplier {cmd.Id} not found");
        var d = cmd.Data;
        entity.Name = d.Name; entity.VatNumber = d.VatNumber; entity.Phone = d.Phone;
        entity.Email = d.Email; entity.AddressLine = d.AddressLine;
        entity.ContactPerson = d.ContactPerson; entity.Notes = d.Notes;
        entity.IsActive = d.IsActive;
        await db.SaveChangesAsync(ct);
    }
}

public record DeleteSupplierCommand(Guid Id) : IRequest;
public class DeleteSupplierHandler(IWorkshopDbContext db) : IRequestHandler<DeleteSupplierCommand>
{
    public async Task Handle(DeleteSupplierCommand cmd, CancellationToken ct)
    {
        var entity = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Supplier {cmd.Id} not found");
        if (await db.InsurancePartLines.AnyAsync(p => p.SupplierId == cmd.Id, ct)
            || await db.RetailPartLines.AnyAsync(p => p.SupplierId == cmd.Id, ct))
            throw new InvalidOperationException("Cannot delete supplier referenced by part orders.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}

public class SupplierUpsertValidator : AbstractValidator<SupplierUpsertDto>
{
    public SupplierUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
public class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{ public CreateSupplierValidator() => RuleFor(x => x.Data).SetValidator(new SupplierUpsertValidator()); }
public class UpdateSupplierValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new SupplierUpsertValidator());
    }
}
