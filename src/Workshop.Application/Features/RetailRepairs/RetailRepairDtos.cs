using Workshop.Domain.Enums;

namespace Workshop.Application.Features.RetailRepairs;

public record RetailRepairDto(
    Guid? Id,
    Guid RetailCaseId,
    DateOnly ScheduledDate,
    TimeOnly? ScheduledTime,
    DateTime? StartDate,
    DateTime? CompletionDate,
    Guid? TechnicianId,
    string? TechnicianName,
    RepairStatus Status,
    DateTime? UpdatedAt);

public record UpsertRetailRepairScheduleDto(
    DateOnly ScheduledDate,
    TimeOnly? ScheduledTime,
    Guid? TechnicianId);

public record CompleteRetailRepairDto(DateTime CompletionDate);
