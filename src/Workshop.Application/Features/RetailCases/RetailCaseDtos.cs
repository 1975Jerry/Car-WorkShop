using Workshop.Domain.Enums;

namespace Workshop.Application.Features.RetailCases;

public record RetailCaseListItemDto(
    Guid Id,
    string CaseNumber,
    RetailCaseStatus Status,
    string CustomerDisplayName,
    string VehiclePlate,
    string VehicleBrandModel,
    string BranchName,
    string? AssignedUserName,
    string WorkType,
    decimal TotalWithVat,
    DateOnly? ScheduledDate,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record RetailCaseDetailDto(
    Guid Id,
    string CaseNumber,
    RetailCaseStatus Status,

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
    Guid? AssignedUserId,
    string? AssignedUserName,

    string WorkType,
    decimal FinalCost,
    decimal VatAmount,
    decimal TotalWithVat,
    DateOnly? ScheduledDate,
    DateTime? CompletedAt,
    string? Notes,

    DateTime CreatedAt,
    DateTime UpdatedAt);

public record RetailCaseUpsertDto(
    Guid CustomerId,
    Guid VehicleId,
    Guid BranchId,
    Guid? AssignedUserId,
    string WorkType,
    decimal FinalCost,
    decimal VatAmount,
    DateOnly? ScheduledDate,
    string? Notes);

public record RetailCaseEventDto(
    Guid Id,
    string? FromStatus,
    string ToStatus,
    string TriggeredByName,
    string? Reason,
    DateTime OccurredAt);
