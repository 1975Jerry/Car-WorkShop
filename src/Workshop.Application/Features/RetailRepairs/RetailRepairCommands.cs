using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.RetailRepairs;

public record UpsertRetailRepairScheduleCommand(
    Guid RetailCaseId,
    UpsertRetailRepairScheduleDto Data) : IRequest<Guid>;

public class UpsertRetailRepairScheduleHandler(IWorkshopDbContext db)
    : IRequestHandler<UpsertRetailRepairScheduleCommand, Guid>
{
    public async Task<Guid> Handle(UpsertRetailRepairScheduleCommand cmd, CancellationToken ct)
    {
        var caseExists = await db.RetailCases.AsNoTracking()
            .AnyAsync(c => c.Id == cmd.RetailCaseId, ct);
        if (!caseExists)
            throw new KeyNotFoundException($"Retail case {cmd.RetailCaseId} not found");

        var repair = await db.RetailRepairs.FirstOrDefaultAsync(r => r.RetailCaseId == cmd.RetailCaseId, ct);
        if (repair is null)
        {
            repair = new RetailRepair
            {
                RetailCaseId = cmd.RetailCaseId,
                Status = RepairStatus.Scheduled
            };
            db.RetailRepairs.Add(repair);
        }
        else if (repair.Status == RepairStatus.Completed)
            throw new InvalidOperationException("Cannot reschedule a completed retail repair.");

        repair.ScheduledDate = cmd.Data.ScheduledDate;
        repair.ScheduledTime = cmd.Data.ScheduledTime;
        repair.TechnicianId = cmd.Data.TechnicianId;

        await db.SaveChangesAsync(ct);
        return repair.Id;
    }
}

public record StartRetailRepairCommand(Guid RetailCaseId) : IRequest;

public class StartRetailRepairHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<StartRetailRepairCommand>
{
    public async Task Handle(StartRetailRepairCommand cmd, CancellationToken ct)
    {
        var repair = await db.RetailRepairs.FirstOrDefaultAsync(r => r.RetailCaseId == cmd.RetailCaseId, ct)
            ?? throw new KeyNotFoundException("Retail repair must be scheduled before it can be started.");
        if (repair.Status == RepairStatus.Completed)
            throw new InvalidOperationException("Retail repair is already completed.");
        if (repair.TechnicianId is null)
            throw new InvalidOperationException("A technician must be assigned before starting the retail repair.");

        repair.Status = RepairStatus.InProgress;
        repair.StartDate ??= clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }
}

public record CompleteRetailRepairCommand(Guid RetailCaseId, CompleteRetailRepairDto Data) : IRequest;

public class CompleteRetailRepairHandler(IWorkshopDbContext db)
    : IRequestHandler<CompleteRetailRepairCommand>
{
    public async Task Handle(CompleteRetailRepairCommand cmd, CancellationToken ct)
    {
        var repair = await db.RetailRepairs.FirstOrDefaultAsync(r => r.RetailCaseId == cmd.RetailCaseId, ct)
            ?? throw new KeyNotFoundException("Retail repair record must exist before it can be completed.");
        if (repair.StartDate is null)
            throw new InvalidOperationException("Retail repair must be started before it can be completed.");

        repair.CompletionDate = cmd.Data.CompletionDate;
        repair.Status = RepairStatus.Completed;
        await db.SaveChangesAsync(ct);
    }
}

public class UpsertRetailRepairScheduleValidator : AbstractValidator<UpsertRetailRepairScheduleCommand>
{
    public UpsertRetailRepairScheduleValidator()
    {
        RuleFor(x => x.RetailCaseId).NotEmpty();
        RuleFor(x => x.Data.ScheduledDate).NotEmpty();
    }
}

public class CompleteRetailRepairValidator : AbstractValidator<CompleteRetailRepairCommand>
{
    public CompleteRetailRepairValidator()
    {
        RuleFor(x => x.RetailCaseId).NotEmpty();
        RuleFor(x => x.Data.CompletionDate).NotEmpty();
    }
}
