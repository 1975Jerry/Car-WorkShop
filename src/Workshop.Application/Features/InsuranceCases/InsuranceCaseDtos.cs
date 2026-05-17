using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsuranceCases;

public record InsuranceCaseListItemDto(
    Guid Id,
    string CaseNumber,
    InsuranceCaseStatus Status,
    CasePriority? Priority,
    string CustomerDisplayName,
    string VehiclePlate,
    string VehicleBrandModel,
    string InsuranceCompanyName,
    string BranchName,
    string? AssignedUserName,
    DateOnly? AccidentDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record InsuranceCaseDetailDto(
    Guid Id,
    string CaseNumber,
    InsuranceCaseStatus Status,
    CasePriority? Priority,

    Guid CustomerId,
    string CustomerDisplayName,
    string CustomerPhone,
    string? CustomerEmail,

    Guid VehicleId,
    string VehiclePlate,
    string VehicleBrand,
    string VehicleModel,
    int? VehicleYear,
    string? VehicleColor,

    Guid BranchId,
    string BranchName,

    Guid InsuranceCompanyId,
    string InsuranceCompanyName,
    string? ClaimNumber,

    Guid? AssessorId,
    string? AssessorName,
    string? AssessorPhone,
    Guid? AdjusterId,
    string? AdjusterName,
    string? AdjusterPhone,
    Guid? AssignedUserId,
    string? AssignedUserName,

    string? DriverFirstName,
    string? DriverLastName,
    string? DriverPhone,
    string? DriverEmail,

    DateOnly? AccidentDate,
    int? MileageAtAssessment,
    DateTime? ClosedAt,
    string? Notes,

    DateTime CreatedAt,
    DateTime UpdatedAt);

public record InsuranceCaseUpsertDto(
    Guid CustomerId,
    Guid VehicleId,
    Guid BranchId,
    Guid InsuranceCompanyId,
    string? ClaimNumber,
    CasePriority? Priority,
    Guid? AssessorId,
    Guid? AdjusterId,
    Guid? AssignedUserId,
    string? DriverFirstName,
    string? DriverLastName,
    string? DriverPhone,
    string? DriverEmail,
    DateOnly? AccidentDate,
    int? MileageAtAssessment,
    string? Notes);

public record CaseEventDto(
    Guid Id,
    string? FromStatus,
    string ToStatus,
    string TriggeredByName,
    string? Reason,
    DateTime OccurredAt);

public record CaseWorkflowSnapshot(
    InsuranceCaseStatus CurrentStatus,
    IReadOnlyList<TriggerOption> AvailableTriggers,
    IReadOnlyList<string> CurrentBlockers);

public record TriggerOption(
    CaseTriggerEvent Trigger,
    string DisplayLabel,
    InsuranceCaseStatus TargetStatus,
    bool CanFire,
    string? BlockerReason);
