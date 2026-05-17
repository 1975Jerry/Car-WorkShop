using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Application.Features.InsuranceCompanies;

public record InsuranceCompanyListItemDto(Guid Id, string Name, string? VatNumber, string? Phone, string? Email,
    int CaseCount, bool IsActive);

public record InsuranceCompanyDetailDto(Guid Id, string Name, string? VatNumber, string? Phone, string? Email,
    string? AddressLine, string? Notes, bool IsActive);

public record InsuranceCompanyUpsertDto(string Name, string? VatNumber, string? Phone, string? Email,
    string? AddressLine, string? Notes, bool IsActive);

public record ListInsuranceCompaniesQuery(string? Search = null, bool? IsActive = null,
    int Page = 1, int PageSize = 25) : IRequest<PagedList<InsuranceCompanyListItemDto>>;

public class ListInsuranceCompaniesHandler(IWorkshopDbContext db)
    : IRequestHandler<ListInsuranceCompaniesQuery, PagedList<InsuranceCompanyListItemDto>>
{
    public async Task<PagedList<InsuranceCompanyListItemDto>> Handle(ListInsuranceCompaniesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var query = db.InsuranceCompanies.AsNoTracking();
        if (q.IsActive.HasValue) query = query.Where(c => c.IsActive == q.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s)
                || (c.VatNumber != null && c.VatNumber.Contains(s)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name)
            .Skip((page - 1) * size).Take(size)
            .Select(c => new InsuranceCompanyListItemDto(
                c.Id, c.Name, c.VatNumber, c.Phone, c.Email,
                db.InsuranceCases.Count(ic => ic.InsuranceCompanyId == c.Id),
                c.IsActive))
            .ToListAsync(ct);
        return new PagedList<InsuranceCompanyListItemDto>(items, page, size, total);
    }
}

public record GetInsuranceCompanyByIdQuery(Guid Id) : IRequest<InsuranceCompanyDetailDto?>;

public class GetInsuranceCompanyByIdHandler(IWorkshopDbContext db)
    : IRequestHandler<GetInsuranceCompanyByIdQuery, InsuranceCompanyDetailDto?>
{
    public async Task<InsuranceCompanyDetailDto?> Handle(GetInsuranceCompanyByIdQuery q, CancellationToken ct) =>
        await db.InsuranceCompanies.AsNoTracking().Where(c => c.Id == q.Id)
            .Select(c => new InsuranceCompanyDetailDto(
                c.Id, c.Name, c.VatNumber, c.Phone, c.Email, c.AddressLine, c.Notes, c.IsActive))
            .FirstOrDefaultAsync(ct);
}

public record CreateInsuranceCompanyCommand(InsuranceCompanyUpsertDto Data) : IRequest<Guid>;

public class CreateInsuranceCompanyHandler(IWorkshopDbContext db)
    : IRequestHandler<CreateInsuranceCompanyCommand, Guid>
{
    public async Task<Guid> Handle(CreateInsuranceCompanyCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        if (await db.InsuranceCompanies.AnyAsync(c => c.Name == d.Name, ct))
            throw new InvalidOperationException($"Insurance company '{d.Name}' already exists");
        var entity = new InsuranceCompany
        {
            Name = d.Name, VatNumber = d.VatNumber, Phone = d.Phone, Email = d.Email,
            AddressLine = d.AddressLine, Notes = d.Notes, IsActive = d.IsActive
        };
        db.InsuranceCompanies.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateInsuranceCompanyCommand(Guid Id, InsuranceCompanyUpsertDto Data) : IRequest;

public class UpdateInsuranceCompanyHandler(IWorkshopDbContext db)
    : IRequestHandler<UpdateInsuranceCompanyCommand>
{
    public async Task Handle(UpdateInsuranceCompanyCommand cmd, CancellationToken ct)
    {
        var entity = await db.InsuranceCompanies.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"InsuranceCompany {cmd.Id} not found");
        var d = cmd.Data;
        if (entity.Name != d.Name && await db.InsuranceCompanies.AnyAsync(c => c.Id != cmd.Id && c.Name == d.Name, ct))
            throw new InvalidOperationException($"Insurance company '{d.Name}' already exists");
        entity.Name = d.Name; entity.VatNumber = d.VatNumber; entity.Phone = d.Phone;
        entity.Email = d.Email; entity.AddressLine = d.AddressLine;
        entity.Notes = d.Notes; entity.IsActive = d.IsActive;
        await db.SaveChangesAsync(ct);
    }
}

public record DeleteInsuranceCompanyCommand(Guid Id) : IRequest;

public class DeleteInsuranceCompanyHandler(IWorkshopDbContext db)
    : IRequestHandler<DeleteInsuranceCompanyCommand>
{
    public async Task Handle(DeleteInsuranceCompanyCommand cmd, CancellationToken ct)
    {
        var entity = await db.InsuranceCompanies.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"InsuranceCompany {cmd.Id} not found");
        if (await db.InsuranceCases.AnyAsync(ic => ic.InsuranceCompanyId == cmd.Id, ct))
            throw new InvalidOperationException("Cannot delete an insurance company referenced by existing cases.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}

public class InsuranceCompanyUpsertValidator : AbstractValidator<InsuranceCompanyUpsertDto>
{
    public InsuranceCompanyUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
public class CreateInsuranceCompanyValidator : AbstractValidator<CreateInsuranceCompanyCommand>
{ public CreateInsuranceCompanyValidator() => RuleFor(x => x.Data).SetValidator(new InsuranceCompanyUpsertValidator()); }
public class UpdateInsuranceCompanyValidator : AbstractValidator<UpdateInsuranceCompanyCommand>
{
    public UpdateInsuranceCompanyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new InsuranceCompanyUpsertValidator());
    }
}
