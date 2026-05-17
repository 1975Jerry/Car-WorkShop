using Workshop.Domain.Enums;

namespace Workshop.Application.Features.CustomerPortal;

public enum PortalCaseKind { Insurance, Retail }

public record MyCaseListItemDto(
    Guid Id,
    PortalCaseKind Kind,
    string CaseNumber,
    string StatusLabel,
    string VehiclePlate,
    string VehicleBrandModel,
    string BranchName,
    DateOnly? ScheduledDate,
    DateTime UpdatedAt);

public record MyCaseDetailDto(
    Guid Id,
    PortalCaseKind Kind,
    string CaseNumber,
    string StatusLabel,

    string VehiclePlate,
    string VehicleBrand,
    string VehicleModel,
    int? VehicleYear,

    string BranchName,
    string? BranchAddress,
    string? BranchPhone,

    string? InsuranceCompanyName,
    string? ClaimNumber,

    string? WorkType,
    DateOnly? AccidentDate,
    DateOnly? ScheduledDate,
    string? Notes,

    decimal AgreedAmount,
    decimal TotalPaid,
    decimal RemainingBalance,
    bool IsFullyPaid,

    DateTime CreatedAt,
    DateTime UpdatedAt);

public record MyCaseEventDto(
    string ToStatusLabel,
    DateTime OccurredAt);

public record MyDocumentDto(
    Guid Id,
    DocumentType DocumentType,
    string FileName,
    string FilePath,
    long SizeBytes,
    DateTime UploadedAt);

public record MyPhotoDto(
    Guid Id,
    string FilePath,
    string Phase,
    DateTime UploadedAt);
