using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Application.Features.Adjusters;

public record AdjusterListItemDto(Guid Id, string FullName, string? Phone, string? Email,
    string? InsuranceCompanyName, bool IsActive);

public record AdjusterDetailDto(Guid Id, string FullName, string? Phone, string? Email,
    Guid? InsuranceCompanyId, string? Notes, bool IsActive);

public record AdjusterUpsertDto(string FullName, string? Phone, string? Email,
    Guid? InsuranceCompanyId, string? Notes, bool IsActive);

public record ListAdjustersQuery(string? Search = null, bool? IsActive = null,
    int Page = 1, int PageSize = 25) : IRequest<PagedList<AdjusterListItemDto>>;

public class ListAdjustersHandler(IWorkshopDbContext db)
    : IRequestHandler<ListAdjustersQuery, PagedList<AdjusterListItemDto>>
{
    public async Task<PagedList<AdjusterListItemDto>> Handle(ListAdjustersQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var query = db.Adjusters.AsNoTracking();
        if (q.IsActive.HasValue) query = query.Where(a => a.IsActive == q.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(a => a.FullName.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(a => a.FullName)
            .Skip((page - 1) * size).Take(size)
            .Select(a => new AdjusterListItemDto(
                a.Id, a.FullName, a.Phone, a.Email,
                a.InsuranceCompany != null ? a.InsuranceCompany.Name : null, a.IsActive))
            .ToListAsync(ct);
        return new PagedList<AdjusterListItemDto>(items, page, size, total);
    }
}

public record GetAdjusterByIdQuery(Guid Id) : IRequest<AdjusterDetailDto?>;
public class GetAdjusterByIdHandler(IWorkshopDbContext db)
    : IRequestHandler<GetAdjusterByIdQuery, AdjusterDetailDto?>
{
    public async Task<AdjusterDetailDto?> Handle(GetAdjusterByIdQuery q, CancellationToken ct) =>
        await db.Adjusters.AsNoTracking().Where(a => a.Id == q.Id)
            .Select(a => new AdjusterDetailDto(
                a.Id, a.FullName, a.Phone, a.Email, a.InsuranceCompanyId, a.Notes, a.IsActive))
            .FirstOrDefaultAsync(ct);
}

public record CreateAdjusterCommand(AdjusterUpsertDto Data) : IRequest<Guid>;
public class CreateAdjusterHandler(IWorkshopDbContext db) : IRequestHandler<CreateAdjusterCommand, Guid>
{
    public async Task<Guid> Handle(CreateAdjusterCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        var entity = new Adjuster
        {
            FullName = d.FullName, Phone = d.Phone, Email = d.Email,
            InsuranceCompanyId = d.InsuranceCompanyId, Notes = d.Notes, IsActive = d.IsActive
        };
        db.Adjusters.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateAdjusterCommand(Guid Id, AdjusterUpsertDto Data) : IRequest;
public class UpdateAdjusterHandler(IWorkshopDbContext db) : IRequestHandler<UpdateAdjusterCommand>
{
    public async Task Handle(UpdateAdjusterCommand cmd, CancellationToken ct)
    {
        var entity = await db.Adjusters.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Adjuster {cmd.Id} not found");
        var d = cmd.Data;
        entity.FullName = d.FullName; entity.Phone = d.Phone; entity.Email = d.Email;
        entity.InsuranceCompanyId = d.InsuranceCompanyId;
        entity.Notes = d.Notes; entity.IsActive = d.IsActive;
        await db.SaveChangesAsync(ct);
    }
}

public record DeleteAdjusterCommand(Guid Id) : IRequest;
public class DeleteAdjusterHandler(IWorkshopDbContext db) : IRequestHandler<DeleteAdjusterCommand>
{
    public async Task Handle(DeleteAdjusterCommand cmd, CancellationToken ct)
    {
        var entity = await db.Adjusters.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Adjuster {cmd.Id} not found");
        if (await db.InsuranceCases.AnyAsync(ic => ic.AdjusterId == cmd.Id, ct))
            throw new InvalidOperationException("Cannot delete adjuster referenced by cases.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}

public class AdjusterUpsertValidator : AbstractValidator<AdjusterUpsertDto>
{
    public AdjusterUpsertValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
public class CreateAdjusterValidator : AbstractValidator<CreateAdjusterCommand>
{ public CreateAdjusterValidator() => RuleFor(x => x.Data).SetValidator(new AdjusterUpsertValidator()); }
public class UpdateAdjusterValidator : AbstractValidator<UpdateAdjusterCommand>
{
    public UpdateAdjusterValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new AdjusterUpsertValidator());
    }
}
