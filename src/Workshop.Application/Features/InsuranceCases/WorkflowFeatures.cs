using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Notifications;
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
    TimeProvider clock,
    INotificationDispatcher notifications,
    ICaseNotificationRecipients recipients)
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

        await NotifyAsync(entity, from, sm.State, ct);
    }

    private async Task NotifyAsync(Domain.Entities.Insurance.InsuranceCase entity, InsuranceCaseStatus from, InsuranceCaseStatus to, CancellationToken ct)
    {
        // Pick audience by the new status: keep the customer informed at every customer-facing
        // step, ping insurer reviewers when the file is in their hands, and ping suppliers
        // when parts ordering opens.
        var audiences = to switch
        {
            InsuranceCaseStatus.InsuranceApproval => CaseAudienceFlags.Customer | CaseAudienceFlags.AssignedStaff | CaseAudienceFlags.InsuranceReviewers,
            InsuranceCaseStatus.CustomerAssignment => CaseAudienceFlags.Customer | CaseAudienceFlags.AssignedStaff,
            InsuranceCaseStatus.PartsApprovalAndOrder => CaseAudienceFlags.Customer | CaseAudienceFlags.AssignedStaff | CaseAudienceFlags.Suppliers,
            InsuranceCaseStatus.RepairScheduling
                or InsuranceCaseStatus.RepairInProgress
                or InsuranceCaseStatus.RepairCompleted
                or InsuranceCaseStatus.Settlement
                or InsuranceCaseStatus.PaymentConfirmed
                or InsuranceCaseStatus.CaseClosed => CaseAudienceFlags.Customer | CaseAudienceFlags.AssignedStaff,
            _ => CaseAudienceFlags.AssignedStaff,
        };

        var to_recipients = await recipients.ResolveAsync(entity.Id, null, audiences, ct);
        if (to_recipients.Count == 0)
            return;

        await notifications.DispatchAsync(new NotificationRequest(
            Kind: NotificationKind.CaseStatusChanged,
            TitleGr: $"Φάκελος {entity.CaseNumber}: {StatusLabelGr(to)}",
            TitleEn: $"Case {entity.CaseNumber}: {to}",
            BodyGr: $"Ο φάκελος προχώρησε από «{StatusLabelGr(from)}» σε «{StatusLabelGr(to)}».",
            BodyEn: $"Case moved from {from} to {to}.",
            Url: $"/cases/insurance/{entity.Id}",
            Recipients: to_recipients), ct);
    }

    private static string StatusLabelGr(InsuranceCaseStatus s) => s switch
    {
        InsuranceCaseStatus.NewCase => "Νέος Φάκελος",
        InsuranceCaseStatus.AssessorAppointment => "Ραντεβού Πραγματογνώμονα",
        InsuranceCaseStatus.Assessment => "Πραγματογνωμοσύνη",
        InsuranceCaseStatus.InsuranceApproval => "Έγκριση Ασφαλιστικής",
        InsuranceCaseStatus.CustomerAssignment => "Εκχώρηση Πελάτη",
        InsuranceCaseStatus.PartsApprovalAndOrder => "Έγκριση & Παραγγελία Ανταλλακτικών",
        InsuranceCaseStatus.RepairScheduling => "Προγραμματισμός Επισκευής",
        InsuranceCaseStatus.RepairInProgress => "Επισκευή σε Εξέλιξη",
        InsuranceCaseStatus.RepairCompleted => "Ολοκλήρωση Επισκευής",
        InsuranceCaseStatus.Settlement => "Εξοφλητική",
        InsuranceCaseStatus.PaymentConfirmed => "Επιβεβαίωση Πληρωμής",
        InsuranceCaseStatus.CaseClosed => "Κλείσιμο Φακέλου",
        _ => s.ToString(),
    };
}
