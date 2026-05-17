using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Photos;

public enum PhotoOwnerKind { Assessment, Repair }

public record PhotoDto(
    Guid Id,
    PhotoOwnerKind OwnerKind,
    Guid OwnerId,
    PhotoPhase Phase,
    string FileName,
    string FilePath,
    string ContentType,
    long SizeBytes,
    string? Caption,
    Guid UploadedById,
    string UploadedByName,
    DateTime UploadedAt);

public record UploadPhotoInput(
    PhotoOwnerKind OwnerKind,
    Guid OwnerId,
    PhotoPhase Phase,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? Caption);
