using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Files;
using Workshop.Domain.Entities.Insurance;

namespace Workshop.Application.Features.Documents;

public record UploadDocumentCommand(UploadDocumentInput Input, Stream Content) : IRequest<Guid>;

public class UploadDocumentHandler(
    IWorkshopDbContext db,
    IFileStore files,
    ICurrentUserService user)
    : IRequestHandler<UploadDocumentCommand, Guid>
{
    public async Task<Guid> Handle(UploadDocumentCommand cmd, CancellationToken ct)
    {
        if (user.UserId is null)
            throw new InvalidOperationException("Must be authenticated to upload a document.");

        var input = cmd.Input;
        var ownerExists = input.OwnerKind switch
        {
            DocumentOwnerKind.InsuranceCase =>
                await db.InsuranceCases.AsNoTracking().AnyAsync(c => c.Id == input.OwnerId, ct),
            DocumentOwnerKind.RetailCase =>
                await db.RetailCases.AsNoTracking().AnyAsync(c => c.Id == input.OwnerId, ct),
            DocumentOwnerKind.Customer =>
                await db.Customers.AsNoTracking().AnyAsync(c => c.Id == input.OwnerId, ct),
            DocumentOwnerKind.Vehicle =>
                await db.Vehicles.AsNoTracking().AnyAsync(v => v.Id == input.OwnerId, ct),
            _ => false
        };
        if (!ownerExists)
            throw new KeyNotFoundException($"{input.OwnerKind} {input.OwnerId} not found");

        var folder = $"docs/{input.OwnerKind.ToString().ToLowerInvariant()}-{input.OwnerId:N}";
        var stored = await files.SaveAsync(folder, input.OriginalFileName, cmd.Content, input.ContentType, ct);

        var doc = new Document
        {
            DocumentType = input.DocumentType,
            FileName = SanitizeFileName(input.OriginalFileName),
            FilePath = stored.RelativePath,
            ContentType = input.ContentType,
            SizeBytes = stored.SizeBytes,
            UploadedById = user.UserId.Value
        };
        switch (input.OwnerKind)
        {
            case DocumentOwnerKind.InsuranceCase: doc.InsuranceCaseId = input.OwnerId; break;
            case DocumentOwnerKind.RetailCase: doc.RetailCaseId = input.OwnerId; break;
            case DocumentOwnerKind.Customer: doc.CustomerId = input.OwnerId; break;
            case DocumentOwnerKind.Vehicle: doc.VehicleId = input.OwnerId; break;
        }

        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        return doc.Id;
    }

    private static string SanitizeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !bad.Contains(c)).ToArray());
    }
}

public record DeleteDocumentCommand(Guid DocumentId) : IRequest;

public class DeleteDocumentHandler(IWorkshopDbContext db, IFileStore files)
    : IRequestHandler<DeleteDocumentCommand>
{
    public async Task Handle(DeleteDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == cmd.DocumentId, ct)
            ?? throw new KeyNotFoundException($"Document {cmd.DocumentId} not found");
        if (doc.SentToInsurance)
            throw new InvalidOperationException("Cannot delete a document already sent to insurance.");

        await files.DeleteAsync(doc.FilePath, ct);
        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
    }
}

public record MarkDocumentSentToInsuranceCommand(Guid DocumentId, bool Sent) : IRequest;

public class MarkDocumentSentToInsuranceHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<MarkDocumentSentToInsuranceCommand>
{
    public async Task Handle(MarkDocumentSentToInsuranceCommand cmd, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == cmd.DocumentId, ct)
            ?? throw new KeyNotFoundException($"Document {cmd.DocumentId} not found");
        doc.SentToInsurance = cmd.Sent;
        doc.SentToInsuranceAt = cmd.Sent ? clock.GetUtcNow().UtcDateTime : null;
        await db.SaveChangesAsync(ct);
    }
}

public class UploadDocumentValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentValidator()
    {
        RuleFor(x => x.Input.OwnerId).NotEmpty();
        RuleFor(x => x.Input.OriginalFileName).NotEmpty();
        RuleFor(x => x.Input.ContentType).NotEmpty();
        RuleFor(x => x.Input.SizeBytes).GreaterThan(0).LessThanOrEqualTo(20L * 1024 * 1024)
            .WithMessage("File must be > 0 bytes and ≤ 20 MB.");
    }
}
