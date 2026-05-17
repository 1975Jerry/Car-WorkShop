using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.CrossCutting;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.RetailCases;

public record CreateRetailCaseCommand(RetailCaseUpsertDto Data) : IRequest<Guid>;

public class CreateRetailCaseHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<CreateRetailCaseCommand, Guid>
{
    public async Task<Guid> Handle(CreateRetailCaseCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;

        // RET-YYYY-NNNN per-year sequence
        var year = clock.GetUtcNow().Year;
        var prefix = $"RET-{year}-";
        var yearCount = await db.RetailCases.Where(c => c.CaseNumber.StartsWith(prefix)).CountAsync(ct);
        var caseNumber = $"{prefix}{(yearCount + 1).ToString("D4")}";
        while (await db.RetailCases.AnyAsync(c => c.CaseNumber == caseNumber, ct))
        {
            yearCount++;
            caseNumber = $"{prefix}{(yearCount + 1).ToString("D4")}";
        }

        var entity = new RetailCase
        {
            CaseNumber = caseNumber,
            CustomerId = d.CustomerId,
            VehicleId = d.VehicleId,
            BranchId = d.BranchId,
            AssignedUserId = d.AssignedUserId,
            WorkType = d.WorkType,
            FinalCost = d.FinalCost,
            VatAmount = d.VatAmount,
            TotalWithVat = RetailCaseCalculator.TotalWithVat(d.FinalCost, d.VatAmount),
            ScheduledDate = d.ScheduledDate,
            Notes = d.Notes,
            Status = RetailCaseStatus.Quoted
        };
        db.RetailCases.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateRetailCaseCommand(Guid Id, RetailCaseUpsertDto Data) : IRequest;

public class UpdateRetailCaseHandler(IWorkshopDbContext db) : IRequestHandler<UpdateRetailCaseCommand>
{
    public async Task Handle(UpdateRetailCaseCommand cmd, CancellationToken ct)
    {
        var entity = await db.RetailCases.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Retail case {cmd.Id} not found");

        // Closed cases are read-only.
        if (entity.Status == RetailCaseStatus.Closed)
            throw new InvalidOperationException("A closed retail case cannot be modified.");

        var d = cmd.Data;
        entity.CustomerId = d.CustomerId;
        entity.VehicleId = d.VehicleId;
        entity.BranchId = d.BranchId;
        entity.AssignedUserId = d.AssignedUserId;
        entity.WorkType = d.WorkType;
        entity.FinalCost = d.FinalCost;
        entity.VatAmount = d.VatAmount;
        entity.TotalWithVat = RetailCaseCalculator.TotalWithVat(d.FinalCost, d.VatAmount);
        entity.ScheduledDate = d.ScheduledDate;
        entity.Notes = d.Notes;

        await db.SaveChangesAsync(ct);
    }
}

public record TransitionRetailCaseCommand(Guid CaseId, RetailCaseStatus Target, string? Reason) : IRequest;

public class TransitionRetailCaseHandler(
    IWorkshopDbContext db,
    ICurrentUserService currentUser,
    TimeProvider clock)
    : IRequestHandler<TransitionRetailCaseCommand>
{
    public async Task Handle(TransitionRetailCaseCommand cmd, CancellationToken ct)
    {
        var entity = await db.RetailCases.FirstOrDefaultAsync(c => c.Id == cmd.CaseId, ct)
            ?? throw new KeyNotFoundException($"Retail case {cmd.CaseId} not found");

        if (currentUser.UserId is null)
            throw new InvalidOperationException("Must be authenticated to transition a retail case.");

        if (!RetailCaseTransitions.IsValid(entity.Status, cmd.Target))
            throw new InvalidOperationException(
                $"Cannot transition retail case from '{entity.Status}' to '{cmd.Target}'.");

        var blocker = await GetBlockerAsync(entity, cmd.Target, ct);
        if (blocker is not null)
            throw new InvalidOperationException(blocker);

        var from = entity.Status;
        entity.Status = cmd.Target;

        if (cmd.Target == RetailCaseStatus.Completed && entity.CompletedAt is null)
            entity.CompletedAt = clock.GetUtcNow().UtcDateTime;

        db.CaseEvents.Add(new CaseEvent
        {
            RetailCaseId = entity.Id,
            FromStatus = from.ToString(),
            ToStatus = cmd.Target.ToString(),
            TriggeredById = currentUser.UserId.Value,
            Reason = cmd.Reason,
            OccurredAt = clock.GetUtcNow().UtcDateTime
        });

        await db.SaveChangesAsync(ct);
    }

    private async Task<string?> GetBlockerAsync(RetailCase c, RetailCaseStatus target, CancellationToken ct)
    {
        switch (target)
        {
            case RetailCaseStatus.InProgress:
                // Need a scheduled repair before work starts.
                var hasSchedule = await db.RetailRepairs.AsNoTracking()
                    .AnyAsync(r => r.RetailCaseId == c.Id, ct);
                if (!hasSchedule)
                    return "A repair must be scheduled before work can start.";
                break;

            case RetailCaseStatus.Completed:
                var repair = await db.RetailRepairs.AsNoTracking()
                    .Where(r => r.RetailCaseId == c.Id)
                    .Select(r => new { r.Status, r.CompletionDate })
                    .FirstOrDefaultAsync(ct);
                if (repair is null || repair.Status != RepairStatus.Completed)
                    return "The repair must be completed before the case can be marked Completed.";
                break;

            case RetailCaseStatus.Paid:
                var totalPaid = await db.Payments.AsNoTracking()
                    .Where(p => p.RetailCaseId == c.Id)
                    .Select(p => (decimal?)p.Amount).ToListAsync(ct);
                if ((totalPaid.Sum(x => x ?? 0m)) < c.TotalWithVat)
                    return "Total payments must cover the case total before marking as Paid.";
                break;

            case RetailCaseStatus.Closed:
                if (c.Status != RetailCaseStatus.Paid)
                    return "Only a paid retail case can be closed.";
                break;
        }
        return null;
    }
}

/// <summary>
/// Allowed transitions for RetailCaseStatus.
/// Quoted → Accepted, Closed (cancel before acceptance)
/// Accepted → InProgress, Closed (cancel)
/// InProgress → Completed
/// Completed → Paid
/// Paid → Closed
/// Closed: terminal
/// </summary>
public static class RetailCaseTransitions
{
    public static bool IsValid(RetailCaseStatus from, RetailCaseStatus to)
    {
        if (from == to) return false;
        return (from, to) switch
        {
            (RetailCaseStatus.Quoted, RetailCaseStatus.Accepted) => true,
            (RetailCaseStatus.Quoted, RetailCaseStatus.Closed) => true,
            (RetailCaseStatus.Accepted, RetailCaseStatus.InProgress) => true,
            (RetailCaseStatus.Accepted, RetailCaseStatus.Closed) => true,
            (RetailCaseStatus.InProgress, RetailCaseStatus.Completed) => true,
            (RetailCaseStatus.Completed, RetailCaseStatus.Paid) => true,
            (RetailCaseStatus.Paid, RetailCaseStatus.Closed) => true,
            _ => false
        };
    }

    public static IReadOnlyList<RetailCaseStatus> AllowedNext(RetailCaseStatus current) =>
        Enum.GetValues<RetailCaseStatus>().Where(s => IsValid(current, s)).ToList();
}

public class RetailCaseUpsertValidator : AbstractValidator<RetailCaseUpsertDto>
{
    public RetailCaseUpsertValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.WorkType).NotEmpty().MaximumLength(300);
        RuleFor(x => x.FinalCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VatAmount).GreaterThanOrEqualTo(0);
    }
}

public class CreateRetailCaseValidator : AbstractValidator<CreateRetailCaseCommand>
{ public CreateRetailCaseValidator() => RuleFor(x => x.Data).SetValidator(new RetailCaseUpsertValidator()); }

public class UpdateRetailCaseValidator : AbstractValidator<UpdateRetailCaseCommand>
{
    public UpdateRetailCaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new RetailCaseUpsertValidator());
    }
}

public class TransitionRetailCaseValidator : AbstractValidator<TransitionRetailCaseCommand>
{
    public TransitionRetailCaseValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
    }
}
