using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Documents;

public record GetDocumentsForCaseQuery(Guid InsuranceCaseId) : IRequest<IReadOnlyList<DocumentDto>>;

public class GetDocumentsForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetDocumentsForCaseQuery, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(GetDocumentsForCaseQuery q, CancellationToken ct)
    {
        var caseId = q.InsuranceCaseId;
        var rows = await db.Documents.AsNoTracking()
            .Where(d => d.InsuranceCaseId == caseId)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.DocumentType,
                d.FileName,
                d.FilePath,
                d.ContentType,
                d.SizeBytes,
                d.UploadedById,
                d.SentToInsurance,
                d.SentToInsuranceAt,
                d.CreatedAt
            })
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.UploadedById).Distinct().ToArray();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(r => new DocumentDto(
            r.Id, DocumentOwnerKind.InsuranceCase, caseId,
            r.DocumentType, r.FileName, r.FilePath, r.ContentType, r.SizeBytes,
            r.UploadedById,
            userNames.TryGetValue(r.UploadedById, out var name) ? name : "",
            r.SentToInsurance, r.SentToInsuranceAt, r.CreatedAt)).ToList();
    }
}

public record GetDocumentsForRetailCaseQuery(Guid RetailCaseId) : IRequest<IReadOnlyList<DocumentDto>>;

public class GetDocumentsForRetailCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetDocumentsForRetailCaseQuery, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(GetDocumentsForRetailCaseQuery q, CancellationToken ct)
    {
        var caseId = q.RetailCaseId;
        var rows = await db.Documents.AsNoTracking()
            .Where(d => d.RetailCaseId == caseId)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.DocumentType,
                d.FileName,
                d.FilePath,
                d.ContentType,
                d.SizeBytes,
                d.UploadedById,
                d.SentToInsurance,
                d.SentToInsuranceAt,
                d.CreatedAt
            })
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.UploadedById).Distinct().ToArray();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(r => new DocumentDto(
            r.Id, DocumentOwnerKind.RetailCase, caseId,
            r.DocumentType, r.FileName, r.FilePath, r.ContentType, r.SizeBytes,
            r.UploadedById,
            userNames.TryGetValue(r.UploadedById, out var name) ? name : "",
            r.SentToInsurance, r.SentToInsuranceAt, r.CreatedAt)).ToList();
    }
}
