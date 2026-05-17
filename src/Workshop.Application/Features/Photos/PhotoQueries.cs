using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Photos;

public record GetPhotosForOwnerQuery(PhotoOwnerKind OwnerKind, Guid OwnerId, PhotoPhase? Phase = null)
    : IRequest<IReadOnlyList<PhotoDto>>;

public class GetPhotosForOwnerHandler(IWorkshopDbContext db)
    : IRequestHandler<GetPhotosForOwnerQuery, IReadOnlyList<PhotoDto>>
{
    public async Task<IReadOnlyList<PhotoDto>> Handle(GetPhotosForOwnerQuery q, CancellationToken ct)
    {
        var ownerId = q.OwnerId;
        var phase = q.Phase;
        var query = q.OwnerKind == PhotoOwnerKind.Assessment
            ? db.Photos.AsNoTracking().Where(p => p.AssessmentId == ownerId)
            : db.Photos.AsNoTracking().Where(p => p.RepairId == ownerId);
        if (phase.HasValue) query = query.Where(p => p.Phase == phase.Value);

        var rows = await query
            .OrderBy(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Phase,
                p.FileName,
                p.FilePath,
                p.ContentType,
                p.SizeBytes,
                p.Caption,
                p.UploadedById,
                p.CreatedAt
            })
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.UploadedById).Distinct().ToArray();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(r => new PhotoDto(
            r.Id, q.OwnerKind, ownerId, r.Phase,
            r.FileName, r.FilePath, r.ContentType, r.SizeBytes,
            r.Caption, r.UploadedById,
            userNames.TryGetValue(r.UploadedById, out var name) ? name : "",
            r.CreatedAt)).ToList();
    }
}

public record GetPhotosForCaseQuery(Guid InsuranceCaseId, PhotoPhase? Phase = null)
    : IRequest<IReadOnlyList<PhotoDto>>;

public class GetPhotosForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetPhotosForCaseQuery, IReadOnlyList<PhotoDto>>
{
    public async Task<IReadOnlyList<PhotoDto>> Handle(GetPhotosForCaseQuery q, CancellationToken ct)
    {
        var assessmentId = await db.Assessments.AsNoTracking()
            .Where(a => a.InsuranceCaseId == q.InsuranceCaseId)
            .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        var repairId = await db.Repairs.AsNoTracking()
            .Where(r => r.InsuranceCaseId == q.InsuranceCaseId)
            .Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);

        var query = db.Photos.AsNoTracking()
            .Where(p =>
                (assessmentId != null && p.AssessmentId == assessmentId) ||
                (repairId != null && p.RepairId == repairId));
        if (q.Phase.HasValue) query = query.Where(p => p.Phase == q.Phase.Value);

        var rows = await query
            .OrderBy(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.AssessmentId,
                p.RepairId,
                p.Phase,
                p.FileName,
                p.FilePath,
                p.ContentType,
                p.SizeBytes,
                p.Caption,
                p.UploadedById,
                p.CreatedAt
            })
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.UploadedById).Distinct().ToArray();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(r => new PhotoDto(
            r.Id,
            r.AssessmentId.HasValue ? PhotoOwnerKind.Assessment : PhotoOwnerKind.Repair,
            r.AssessmentId ?? r.RepairId ?? Guid.Empty,
            r.Phase, r.FileName, r.FilePath, r.ContentType, r.SizeBytes,
            r.Caption, r.UploadedById,
            userNames.TryGetValue(r.UploadedById, out var name) ? name : "",
            r.CreatedAt)).ToList();
    }
}
