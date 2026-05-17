using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Documents;

public enum DocumentOwnerKind { InsuranceCase, RetailCase, Customer, Vehicle }

public record DocumentDto(
    Guid Id,
    DocumentOwnerKind OwnerKind,
    Guid OwnerId,
    DocumentType DocumentType,
    string FileName,
    string FilePath,
    string ContentType,
    long SizeBytes,
    Guid UploadedById,
    string UploadedByName,
    bool SentToInsurance,
    DateTime? SentToInsuranceAt,
    DateTime UploadedAt);

public record UploadDocumentInput(
    DocumentOwnerKind OwnerKind,
    Guid OwnerId,
    DocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);
