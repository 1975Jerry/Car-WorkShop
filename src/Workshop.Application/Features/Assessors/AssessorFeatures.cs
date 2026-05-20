using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Models;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Application.Features.Assessors;

public record AssessorListItemDto(Guid Id, string FullName, string? Phone, string? Email,
    string? LicenseNumber, string? InsuranceCompanyName, bool IsActive);

public record AssessorDetailDto(Guid Id, string FullName, string? Phone, string? Email,
    string? LicenseNumber, Guid? InsuranceCompanyId, string? Notes, bool IsActive);

public record AssessorUpsertDto(string FullName, string? Phone, string? Email,
    string? LicenseNumber, Guid? InsuranceCompanyId, string? Notes, bool IsActive);

public record ListAssessorsQuery(string? Search = null, bool? IsActive = null,
    int Page = 1, int PageSize = 25) : IRequest<PagedList<AssessorListItemDto>>;

public class ListAssessorsHandler(IWorkshopDbContext db)
    : IRequestHandler<ListAssessorsQuery, PagedList<AssessorListItemDto>>
{
    public async Task<PagedList<AssessorListItemDto>> Handle(ListAssessorsQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var query = db.Assessors.AsNoTracking();
        if (q.IsActive.HasValue) query = query.Where(a => a.IsActive == q.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(a => a.FullName.ToLower().Contains(s)
                || (a.LicenseNumber != null && a.LicenseNumber.ToLower().Contains(s))
                || (a.Phone != null && a.Phone.Contains(s))
                || (a.Email != null && a.Email.ToLower().Contains(s))
                || (a.Notes != null && a.Notes.ToLower().Contains(s))
                || (a.InsuranceCompany != null && a.InsuranceCompany.Name.ToLower().Contains(s)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(a => a.FullName)
            .Skip((page - 1) * size).Take(size)
            .Select(a => new AssessorListItemDto(
                a.Id, a.FullName, a.Phone, a.Email, a.LicenseNumber,
                a.InsuranceCompany != null ? a.InsuranceCompany.Name : null, a.IsActive))
            .ToListAsync(ct);
        return new PagedList<AssessorListItemDto>(items, page, size, total);
    }
}

public record GetAssessorByIdQuery(Guid Id) : IRequest<AssessorDetailDto?>;
public class GetAssessorByIdHandler(IWorkshopDbContext db)
    : IRequestHandler<GetAssessorByIdQuery, AssessorDetailDto?>
{
    public async Task<AssessorDetailDto?> Handle(GetAssessorByIdQuery q, CancellationToken ct) =>
        await db.Assessors.AsNoTracking().Where(a => a.Id == q.Id)
            .Select(a => new AssessorDetailDto(
                a.Id, a.FullName, a.Phone, a.Email, a.LicenseNumber,
                a.InsuranceCompanyId, a.Notes, a.IsActive))
            .FirstOrDefaultAsync(ct);
}

public record CreateAssessorCommand(AssessorUpsertDto Data) : IRequest<Guid>;
public class CreateAssessorHandler(IWorkshopDbContext db) : IRequestHandler<CreateAssessorCommand, Guid>
{
    public async Task<Guid> Handle(CreateAssessorCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        var entity = new Assessor
        {
            FullName = d.FullName, Phone = d.Phone, Email = d.Email,
            LicenseNumber = d.LicenseNumber, InsuranceCompanyId = d.InsuranceCompanyId,
            Notes = d.Notes, IsActive = d.IsActive
        };
        db.Assessors.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateAssessorCommand(Guid Id, AssessorUpsertDto Data) : IRequest;
public class UpdateAssessorHandler(IWorkshopDbContext db) : IRequestHandler<UpdateAssessorCommand>
{
    public async Task Handle(UpdateAssessorCommand cmd, CancellationToken ct)
    {
        var entity = await db.Assessors.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Assessor {cmd.Id} not found");
        var d = cmd.Data;
        entity.FullName = d.FullName; entity.Phone = d.Phone; entity.Email = d.Email;
        entity.LicenseNumber = d.LicenseNumber; entity.InsuranceCompanyId = d.InsuranceCompanyId;
        entity.Notes = d.Notes; entity.IsActive = d.IsActive;
        await db.SaveChangesAsync(ct);
    }
}

public record DeleteAssessorCommand(Guid Id) : IRequest;
public class DeleteAssessorHandler(IWorkshopDbContext db) : IRequestHandler<DeleteAssessorCommand>
{
    public async Task Handle(DeleteAssessorCommand cmd, CancellationToken ct)
    {
        var entity = await db.Assessors.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Assessor {cmd.Id} not found");
        if (await db.InsuranceCases.AnyAsync(ic => ic.AssessorId == cmd.Id, ct))
            throw new InvalidOperationException("Cannot delete assessor referenced by cases.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}

public class AssessorUpsertValidator : AbstractValidator<AssessorUpsertDto>
{
    public AssessorUpsertValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
public class CreateAssessorValidator : AbstractValidator<CreateAssessorCommand>
{ public CreateAssessorValidator() => RuleFor(x => x.Data).SetValidator(new AssessorUpsertValidator()); }
public class UpdateAssessorValidator : AbstractValidator<UpdateAssessorCommand>
{
    public UpdateAssessorValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new AssessorUpsertValidator());
    }
}
