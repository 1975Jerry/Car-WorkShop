using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Repairs;

public record RepairDto(
    Guid? Id,
    Guid InsuranceCaseId,
    DateOnly ScheduledDate,
    TimeOnly? ScheduledTime,
    DateTime? StartDate,
    DateTime? CompletionDate,
    Guid? TechnicianId,
    string? TechnicianName,
    RepairStatus Status,
    bool IntermediateInspectionDone,
    string? Notes,
    DateTime? UpdatedAt);

public record UpsertRepairScheduleDto(
    DateOnly ScheduledDate,
    TimeOnly? ScheduledTime,
    Guid? TechnicianId,
    string? Notes);

public record CompleteRepairDto(
    DateTime CompletionDate,
    string? Notes);
