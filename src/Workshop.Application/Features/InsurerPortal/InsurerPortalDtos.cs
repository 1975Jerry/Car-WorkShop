using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsurerPortal;

public record InsurerCaseListItemDto(
    Guid Id,
    string CaseNumber,
    InsuranceCaseStatus Status,
    ApprovalStatus? ApprovalStatus,
    decimal? ApprovedAmount,
    string VehiclePlate,
    string VehicleBrandModel,
    string? ClaimNumber,
    string CustomerDisplayName,
    DateOnly? AccidentDate,
    DateTime UpdatedAt);

public record InsurerCaseDetailDto(
    Guid Id,
    string CaseNumber,
    InsuranceCaseStatus Status,
    string VehiclePlate,
    string VehicleBrand,
    string VehicleModel,
    int? VehicleYear,
    string? VehicleColor,
    string CustomerDisplayName,
    string CustomerPhone,
    string? CustomerVat,
    string? ClaimNumber,
    DateOnly? AccidentDate,
    int? MileageAtAssessment,

    string? DriverFirstName,
    string? DriverLastName,
    string? DriverPhone,

    InsurerQuoteSummary? Quote,
    InsurerApprovalSummary? Approval,
    IReadOnlyList<InsurerWorkItemRow> WorkItems,
    IReadOnlyList<InsurerPartRow> Parts);

public record InsurerQuoteSummary(
    Guid QuoteId,
    string QuoteNumber,
    DateOnly IssueDate,
    decimal LaborSubtotal,
    decimal PartsSubtotal,
    decimal Subtotal,
    decimal VatRate,
    decimal VatAmount,
    decimal Total,
    string? PdfPath);

public record InsurerApprovalSummary(
    Guid Id,
    bool LiabilityAccepted,
    bool CustomerParticipation,
    decimal? ParticipationAmount,
    decimal ApprovedAmount,
    DateOnly ApprovalDate,
    ApprovalStatus ApprovalStatus,
    string? Notes,
    DateTime UpdatedAt);

public record InsurerWorkItemRow(
    string? PanelCode,
    string Description,
    decimal Total);

public record InsurerPartRow(
    string PartName,
    PartType PartType,
    decimal Quantity,
    decimal UnitCost,
    decimal Total);

public record InsurerSentDocumentDto(
    Guid Id,
    DocumentType DocumentType,
    string FileName,
    string FilePath,
    long SizeBytes,
    DateTime SentAt);

public record InsurerDecisionDto(
    decimal ApprovedAmount,
    string? Notes);
