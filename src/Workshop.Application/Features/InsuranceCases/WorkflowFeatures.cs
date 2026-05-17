using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.CrossCutting;
using Workshop.Domain.Enums;
using Workshop.Domain.Workflows;

namespace Workshop.Application.Features.InsuranceCases;

public record GetCaseWorkflowSnapshotQuery(Guid CaseId) : IRequest<CaseWorkflowSnapshot>;

public class GetCaseWorkflowSnapshotHandler(IWorkshopDbContext db, ICaseGuardContextBuilder ctxBuilder)
    : IRequestHandler<GetCaseWorkflowSnapshotQuery, CaseWorkflowSnapshot>
{
    public async Task<CaseWorkflowSnapshot> Handle(GetCaseWorkflowSnapshotQuery q, CancellationToken ct)
    {
        var status = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == q.CaseId)
            .Select(c => (InsuranceCaseStatus?)c.Status)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Case {q.CaseId} not found");

        var context = await ctxBuilder.BuildAsync(q.CaseId, ct);

        // Probe every trigger from current state and report whether it can fire
        var sm = new InsuranceCaseStateMachine(status, () => context);
        var allTriggers = Enum.GetValues<CaseTriggerEvent>();
        var triggers = new List<TriggerOption>();

        foreach (var t in allTriggers)
        {
            var canFire = sm.CanFire(t);
            // A simulation machine to detect target state of a permitted transition.
            // Stateless doesn't expose target without firing; we approximate by
            // forking a transient machine for each trigger.
            InsuranceCaseStatus targetStatus = status;
            string? blocker = null;
            if (canFire)
            {
                var probe = new InsuranceCaseStateMachine(status, () => context);
                probe.Fire(t);
                targetStatus = probe.State;
            }
            else
            {
                blocker = sm.WhyCannotFire(t);
                if (blocker.StartsWith("No transition")) continue; // not applicable from this state, skip entirely
            }

            triggers.Add(new TriggerOption(t, FriendlyLabel(t), targetStatus, canFire, blocker));
        }

        var blockers = triggers
            .Where(t => !t.CanFire && t.BlockerReason is not null)
            .Select(t => $"{FriendlyLabel(t.Trigger)}: {t.BlockerReason}")
            .ToList();

        return new CaseWorkflowSnapshot(status, triggers, blockers);
    }

    private static string FriendlyLabel(CaseTriggerEvent t) => t switch
    {
        CaseTriggerEvent.BookAssessorAppointment => "Book assessor appointment",
        CaseTriggerEvent.CompleteAssessment => "Complete assessment",
        CaseTriggerEvent.SubmitForInsuranceApproval => "Submit for insurance approval",
        CaseTriggerEvent.ApprovalReceived => "Approval received",
        CaseTriggerEvent.ApprovalRejected => "Approval rejected",
        CaseTriggerEvent.CustomerAccepts => "Customer accepts",
        CaseTriggerEvent.AllPartsReceived => "All parts received",
        CaseTriggerEvent.StartRepair => "Start repair",
        CaseTriggerEvent.CompleteRepair => "Complete repair",
        CaseTriggerEvent.IssueSettlement => "Issue settlement",
        CaseTriggerEvent.ConfirmPayment => "Confirm payment",
        CaseTriggerEvent.CloseCase => "Close case",
        CaseTriggerEvent.Cancel => "Cancel case",
        _ => t.ToString()
    };
}

public record TransitionInsuranceCaseCommand(Guid CaseId, CaseTriggerEvent Trigger, string? Reason) : IRequest;

public class TransitionInsuranceCaseHandler(
    IWorkshopDbContext db,
    ICaseGuardContextBuilder ctxBuilder,
    ICurrentUserService currentUser,
    TimeProvider clock)
    : IRequestHandler<TransitionInsuranceCaseCommand>
{
    public async Task Handle(TransitionInsuranceCaseCommand cmd, CancellationToken ct)
    {
        var entity = await db.InsuranceCases.FirstOrDefaultAsync(c => c.Id == cmd.CaseId, ct)
            ?? throw new KeyNotFoundException($"Case {cmd.CaseId} not found");

        if (currentUser.UserId is null)
            throw new InvalidOperationException("Must be authenticated to transition a case.");

        var context = await ctxBuilder.BuildAsync(cmd.CaseId, ct);
        var sm = new InsuranceCaseStateMachine(entity.Status, () => context);

        if (!sm.CanFire(cmd.Trigger))
            throw new InvalidOperationException(sm.WhyCannotFire(cmd.Trigger));

        var from = entity.Status;
        sm.Fire(cmd.Trigger);
        entity.Status = sm.State;

        if (sm.State == InsuranceCaseStatus.CaseClosed && entity.ClosedAt is null)
            entity.ClosedAt = clock.GetUtcNow().UtcDateTime;

        db.CaseEvents.Add(new CaseEvent
        {
            InsuranceCaseId = entity.Id,
            FromStatus = from.ToString(),
            ToStatus = sm.State.ToString(),
            TriggeredById = currentUser.UserId.Value,
            Reason = cmd.Reason,
            OccurredAt = clock.GetUtcNow().UtcDateTime
        });

        await db.SaveChangesAsync(ct);
    }
}
