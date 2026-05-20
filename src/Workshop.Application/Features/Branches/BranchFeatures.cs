using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Application.Features.Branches;

public record BranchListItemDto(Guid Id, string Code, string Name, string City, string? Phone,
    string? WarehouseName, bool IsActive);

public record BranchDetailDto(Guid Id, string Name, string Code, string AddressLine, string City,
    string? PostalCode, string? Phone, bool IsActive,
    string WarehouseName, string? WarehouseDescription);

public record BranchUpsertDto(string Name, string Code, string AddressLine, string City,
    string? PostalCode, string? Phone, bool IsActive,
    string WarehouseName, string? WarehouseDescription);

public record ListBranchesQuery(string? Search = null, bool? IsActive = null,
    int Page = 1, int PageSize = 25) : IRequest<PagedList<BranchListItemDto>>;

public class ListBranchesHandler(IWorkshopDbContext db)
    : IRequestHandler<ListBranchesQuery, PagedList<BranchListItemDto>>
{
    public async Task<PagedList<BranchListItemDto>> Handle(ListBranchesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var query = db.Branches.AsNoTracking();
        if (q.IsActive.HasValue) query = query.Where(b => b.IsActive == q.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(b =>
                b.Name.ToLower().Contains(s) ||
                b.Code.ToLower().Contains(s) ||
                b.City.ToLower().Contains(s) ||
                b.AddressLine.ToLower().Contains(s) ||
                (b.PostalCode != null && b.PostalCode.Contains(s)) ||
                (b.Phone != null && b.Phone.Contains(s)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(b => b.Name)
            .Skip((page - 1) * size).Take(size)
            .Select(b => new BranchListItemDto(
                b.Id, b.Code, b.Name, b.City, b.Phone,
                b.Warehouse != null ? b.Warehouse.Name : null, b.IsActive))
            .ToListAsync(ct);
        return new PagedList<BranchListItemDto>(items, page, size, total);
    }
}

public record GetBranchByIdQuery(Guid Id) : IRequest<BranchDetailDto?>;

public class GetBranchByIdHandler(IWorkshopDbContext db)
    : IRequestHandler<GetBranchByIdQuery, BranchDetailDto?>
{
    public async Task<BranchDetailDto?> Handle(GetBranchByIdQuery q, CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(b => b.Id == q.Id)
            .Select(b => new BranchDetailDto(
                b.Id, b.Name, b.Code, b.AddressLine, b.City, b.PostalCode, b.Phone, b.IsActive,
                b.Warehouse != null ? b.Warehouse.Name : "Αποθήκη",
                b.Warehouse != null ? b.Warehouse.Description : null))
            .FirstOrDefaultAsync(ct);
}

public record CreateBranchCommand(BranchUpsertDto Data) : IRequest<Guid>;

public class CreateBranchHandler(IWorkshopDbContext db) : IRequestHandler<CreateBranchCommand, Guid>
{
    public async Task<Guid> Handle(CreateBranchCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        if (await db.Branches.AnyAsync(b => b.Code == d.Code, ct))
            throw new InvalidOperationException($"Branch code {d.Code} already exists");
        var entity = new Branch
        {
            Name = d.Name, Code = d.Code, AddressLine = d.AddressLine, City = d.City,
            PostalCode = d.PostalCode, Phone = d.Phone, IsActive = d.IsActive,
            Warehouse = new Warehouse { Name = d.WarehouseName, Description = d.WarehouseDescription }
        };
        db.Branches.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateBranchCommand(Guid Id, BranchUpsertDto Data) : IRequest;

public class UpdateBranchHandler(IWorkshopDbContext db) : IRequestHandler<UpdateBranchCommand>
{
    public async Task Handle(UpdateBranchCommand cmd, CancellationToken ct)
    {
        var entity = await db.Branches.Include(b => b.Warehouse)
            .FirstOrDefaultAsync(b => b.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Branch {cmd.Id} not found");

        var d = cmd.Data;
        if (entity.Code != d.Code && await db.Branches.AnyAsync(b => b.Id != cmd.Id && b.Code == d.Code, ct))
            throw new InvalidOperationException($"Branch code {d.Code} already exists");

        entity.Name = d.Name; entity.Code = d.Code; entity.AddressLine = d.AddressLine;
        entity.City = d.City; entity.PostalCode = d.PostalCode; entity.Phone = d.Phone;
        entity.IsActive = d.IsActive;
        if (entity.Warehouse is null)
            entity.Warehouse = new Warehouse { Name = d.WarehouseName, Description = d.WarehouseDescription };
        else
        {
            entity.Warehouse.Name = d.WarehouseName;
            entity.Warehouse.Description = d.WarehouseDescription;
        }
        await db.SaveChangesAsync(ct);
    }
}

public record DeleteBranchCommand(Guid Id) : IRequest;

public class DeleteBranchHandler(IWorkshopDbContext db) : IRequestHandler<DeleteBranchCommand>
{
    public async Task Handle(DeleteBranchCommand cmd, CancellationToken ct)
    {
        var entity = await db.Branches.FirstOrDefaultAsync(b => b.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Branch {cmd.Id} not found");

        var hasCases = await db.InsuranceCases.AnyAsync(c => c.BranchId == cmd.Id, ct)
            || await db.RetailCases.AnyAsync(c => c.BranchId == cmd.Id, ct);
        if (hasCases)
            throw new InvalidOperationException("Cannot delete a branch with cases. Deactivate it instead.");

        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}

public class BranchUpsertValidator : AbstractValidator<BranchUpsertDto>
{
    public BranchUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20)
            .Matches("^[A-Z0-9_-]+$").WithMessage("Code must be uppercase letters, digits, _ or -");
        RuleFor(x => x.AddressLine).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.WarehouseName).NotEmpty();
    }
}
public class CreateBranchValidator : AbstractValidator<CreateBranchCommand>
{ public CreateBranchValidator() => RuleFor(x => x.Data).SetValidator(new BranchUpsertValidator()); }
public class UpdateBranchValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new BranchUpsertValidator());
    }
}
