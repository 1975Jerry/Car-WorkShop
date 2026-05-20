using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Notifications;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Repairs;

public record UpsertRepairScheduleCommand(Guid InsuranceCaseId, UpsertRepairScheduleDto Data) : IRequest<Guid>;

public class UpsertRepairScheduleHandler(
    IWorkshopDbContext db,
    INotificationDispatcher notifications,
    ICaseNotificationRecipients recipients)
    : IRequestHandler<UpsertRepairScheduleCommand, Guid>
{
    public async Task<Guid> Handle(UpsertRepairScheduleCommand cmd, CancellationToken ct)
    {
        var caseInfo = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == cmd.InsuranceCaseId)
            .Select(c => new { c.CaseNumber })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Insurance case {cmd.InsuranceCaseId} not found");

        var repair = await db.Repairs.FirstOrDefaultAsync(r => r.InsuranceCaseId == cmd.InsuranceCaseId, ct);
        if (repair is null)
        {
            repair = new Repair
            {
                InsuranceCaseId = cmd.InsuranceCaseId,
                Status = RepairStatus.Scheduled
            };
            db.Repairs.Add(repair);
        }
        // Can't reschedule a finished repair.
        else if (repair.Status == RepairStatus.Completed)
            throw new InvalidOperationException("Cannot reschedule a completed repair.");

        repair.ScheduledDate = cmd.Data.ScheduledDate;
        repair.ScheduledTime = cmd.Data.ScheduledTime;
        repair.TechnicianId = cmd.Data.TechnicianId;
        if (!string.IsNullOrWhiteSpace(cmd.Data.Notes))
            repair.Notes = cmd.Data.Notes;

        await db.SaveChangesAsync(ct);

        var to = await recipients.ResolveAsync(
            cmd.InsuranceCaseId, null,
            CaseAudienceFlags.Customer | CaseAudienceFlags.AssignedStaff,
            ct);
        if (to.Count > 0)
        {
            var when = repair.ScheduledTime is { } t
                ? $"{repair.ScheduledDate:dd/MM/yyyy} {t:HH:mm}"
                : repair.ScheduledDate.ToString("dd/MM/yyyy");
            await notifications.DispatchAsync(new NotificationRequest(
                Kind: NotificationKind.RepairScheduled,
                TitleGr: $"Επισκευή {caseInfo.CaseNumber}: {when}",
                TitleEn: $"Repair {caseInfo.CaseNumber}: {when}",
                BodyGr: $"Προγραμματίστηκε επισκευή στις {when}.",
                BodyEn: $"Repair scheduled for {when}.",
                Url: $"/cases/insurance/{cmd.InsuranceCaseId}",
                Recipients: to), ct);
        }

        return repair.Id;
    }
}

public record MarkIntermediateInspectionCommand(Guid InsuranceCaseId, bool Done) : IRequest;

public class MarkIntermediateInspectionHandler(IWorkshopDbContext db)
    : IRequestHandler<MarkIntermediateInspectionCommand>
{
    public async Task Handle(MarkIntermediateInspectionCommand cmd, CancellationToken ct)
    {
        var repair = await db.Repairs.FirstOrDefaultAsync(r => r.InsuranceCaseId == cmd.InsuranceCaseId, ct)
            ?? throw new KeyNotFoundException("Repair must be scheduled before intermediate inspection can be recorded.");
        repair.IntermediateInspectionDone = cmd.Done;
        await db.SaveChangesAsync(ct);
    }
}

public record StartRepairCommand(Guid InsuranceCaseId) : IRequest;

public class StartRepairHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<StartRepairCommand>
{
    public async Task Handle(StartRepairCommand cmd, CancellationToken ct)
    {
        var repair = await db.Repairs.FirstOrDefaultAsync(r => r.InsuranceCaseId == cmd.InsuranceCaseId, ct)
            ?? throw new KeyNotFoundException("Repair must be scheduled before it can be started.");
        if (repair.Status == RepairStatus.Completed)
            throw new InvalidOperationException("Repair is already completed.");
        if (repair.TechnicianId is null)
            throw new InvalidOperationException("A technician must be assigned before starting the repair.");

        repair.Status = RepairStatus.InProgress;
        repair.StartDate ??= clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }
}

public record CompleteRepairCommand(Guid InsuranceCaseId, CompleteRepairDto Data) : IRequest;

public class CompleteRepairHandler(IWorkshopDbContext db)
    : IRequestHandler<CompleteRepairCommand>
{
    public async Task Handle(CompleteRepairCommand cmd, CancellationToken ct)
    {
        var repair = await db.Repairs.FirstOrDefaultAsync(r => r.InsuranceCaseId == cmd.InsuranceCaseId, ct)
            ?? throw new KeyNotFoundException("Repair record must exist before it can be completed.");
        if (repair.StartDate is null)
            throw new InvalidOperationException("Repair must be started before it can be completed.");

        repair.CompletionDate = cmd.Data.CompletionDate;
        repair.Status = RepairStatus.Completed;
        if (!string.IsNullOrWhiteSpace(cmd.Data.Notes))
            repair.Notes = cmd.Data.Notes;
        await db.SaveChangesAsync(ct);
    }
}

public class UpsertRepairScheduleValidator : AbstractValidator<UpsertRepairScheduleCommand>
{
    public UpsertRepairScheduleValidator()
    {
        RuleFor(x => x.InsuranceCaseId).NotEmpty();
        RuleFor(x => x.Data.ScheduledDate).NotEmpty();
    }
}

public class CompleteRepairValidator : AbstractValidator<CompleteRepairCommand>
{
    public CompleteRepairValidator()
    {
        RuleFor(x => x.InsuranceCaseId).NotEmpty();
        RuleFor(x => x.Data.CompletionDate).NotEmpty();
    }
}
