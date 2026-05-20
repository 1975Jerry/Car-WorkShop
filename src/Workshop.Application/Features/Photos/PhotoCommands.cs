using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Files;
using Workshop.Domain.Entities.Insurance;

namespace Workshop.Application.Features.Photos;

public record UploadPhotoCommand(
    UploadPhotoInput Input,
    Stream Content) : IRequest<Guid>;

public class UploadPhotoHandler(
    IWorkshopDbContext db,
    IFileStore files,
    ICurrentUserService user)
    : IRequestHandler<UploadPhotoCommand, Guid>
{
    public async Task<Guid> Handle(UploadPhotoCommand cmd, CancellationToken ct)
    {
        if (user.UserId is null)
            throw new InvalidOperationException("Must be authenticated to upload a photo.");

        var input = cmd.Input;
        var ownerExists = input.OwnerKind switch
        {
            PhotoOwnerKind.Assessment =>
                await db.Assessments.AsNoTracking().AnyAsync(a => a.Id == input.OwnerId, ct),
            PhotoOwnerKind.Repair =>
                await db.Repairs.AsNoTracking().AnyAsync(r => r.Id == input.OwnerId, ct),
            _ => false
        };
        if (!ownerExists)
            throw new KeyNotFoundException($"{input.OwnerKind} {input.OwnerId} not found");

        var folder = $"photos/{input.OwnerKind.ToString().ToLowerInvariant()}-{input.OwnerId:N}";
        var stored = await files.SaveAsync(folder, input.OriginalFileName, cmd.Content, input.ContentType, ct);

        var photo = new Photo
        {
            Phase = input.Phase,
            FileName = SanitizeFileName(input.OriginalFileName),
            FilePath = stored.RelativePath,
            ContentType = input.ContentType,
            SizeBytes = stored.SizeBytes,
            Caption = input.Caption,
            UploadedById = user.UserId.Value
        };
        if (input.OwnerKind == PhotoOwnerKind.Assessment) photo.AssessmentId = input.OwnerId;
        else photo.RepairId = input.OwnerId;

        db.Photos.Add(photo);
        await db.SaveChangesAsync(ct);
        return photo.Id;
    }

    private static string SanitizeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !bad.Contains(c)).ToArray());
    }
}

public record DeletePhotoCommand(Guid PhotoId) : IRequest;

public class DeletePhotoHandler(IWorkshopDbContext db, IFileStore files)
    : IRequestHandler<DeletePhotoCommand>
{
    public async Task Handle(DeletePhotoCommand cmd, CancellationToken ct)
    {
        var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == cmd.PhotoId, ct)
            ?? throw new KeyNotFoundException($"Photo {cmd.PhotoId} not found");

        await files.DeleteAsync(photo.FilePath, ct);
        db.Photos.Remove(photo);
        await db.SaveChangesAsync(ct);
    }
}

public class UploadPhotoValidator : AbstractValidator<UploadPhotoCommand>
{
    public UploadPhotoValidator()
    {
        RuleFor(x => x.Input.OwnerId).NotEmpty();
        RuleFor(x => x.Input.OriginalFileName).NotEmpty();
        RuleFor(x => x.Input.ContentType).NotEmpty();
        RuleFor(x => x.Input.SizeBytes).GreaterThan(0).LessThanOrEqualTo(20L * 1024 * 1024)
            .WithMessage("File must be > 0 bytes and ≤ 20 MB.");
    }
}
