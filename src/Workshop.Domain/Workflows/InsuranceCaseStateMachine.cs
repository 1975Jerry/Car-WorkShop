using Stateless;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Workflows;

public class InsuranceCaseStateMachine
{
    private readonly StateMachine<InsuranceCaseStatus, CaseTriggerEvent> _machine;
    private readonly Func<CaseGuardContext> _contextProvider;

    public InsuranceCaseStateMachine(
        InsuranceCaseStatus initialState,
        Func<CaseGuardContext> contextProvider)
    {
        _contextProvider = contextProvider;
        _machine = new StateMachine<InsuranceCaseStatus, CaseTriggerEvent>(initialState);
        Configure();
    }

    public InsuranceCaseStatus State => _machine.State;

    public bool CanFire(CaseTriggerEvent trigger) => _machine.CanFire(trigger);

    public IEnumerable<CaseTriggerEvent> PermittedTriggers => _machine.PermittedTriggers;

    public void Fire(CaseTriggerEvent trigger)
    {
        if (!_machine.CanFire(trigger))
        {
            var reason = WhyCannotFire(trigger);
            throw new InvalidOperationException(
                $"Cannot fire '{trigger}' from state '{_machine.State}'. {reason}");
        }
        _machine.Fire(trigger);
    }

    public string WhyCannotFire(CaseTriggerEvent trigger)
    {
        var ctx = _contextProvider();
        return (_machine.State, trigger) switch
        {
            (InsuranceCaseStatus.NewCase, CaseTriggerEvent.BookAssessorAppointment) =>
                BuildReason(
                    (!ctx.HasAssessorAssigned, "Assessor must be assigned"),
                    (!ctx.HasAccidentDate, "Accident date must be set"),
                    (ctx.IntakePhotoCount < 1, "At least 1 intake photo is required")),

            (InsuranceCaseStatus.AssessorAppointment, CaseTriggerEvent.CompleteAssessment) =>
                BuildReason(
                    (!ctx.HasAssessmentCompleted, "Assessment record with date, costs, and agreed amount is required"),
                    (ctx.WorkItemCount < 1, "At least 1 WorkItem is required")),

            (InsuranceCaseStatus.Assessment, CaseTriggerEvent.SubmitForInsuranceApproval) =>
                BuildReason(
                    (!ctx.HasCurrentQuote, "A current Quote with PDF must be generated"),
                    (!ctx.HasRequiredDocumentsForApprovalSubmission, "Required documents (CaseForm, InsuranceForm) must be uploaded")),

            (InsuranceCaseStatus.InsuranceApproval, CaseTriggerEvent.ApprovalReceived) =>
                BuildReason(
                    (!ctx.HasApproval, "An InsuranceApproval record must exist"),
                    (!ctx.ApprovalIsPositive, "Approval status must be Approved or PartialApproval")),

            (InsuranceCaseStatus.InsuranceApproval, CaseTriggerEvent.ApprovalRejected) =>
                BuildReason((!ctx.ApprovalIsRejected, "Approval status must be Rejected")),

            (InsuranceCaseStatus.CustomerAssignment, CaseTriggerEvent.CustomerAccepts) =>
                BuildReason((!ctx.CustomerParticipationConfirmed, "Customer participation must be confirmed")),

            (InsuranceCaseStatus.PartsApprovalAndOrder, CaseTriggerEvent.AllPartsReceived) =>
                BuildReason((!ctx.AllPartsReceivedOrNotNeeded, "All required parts must be Received (or marked not needed)")),

            (InsuranceCaseStatus.RepairScheduling, CaseTriggerEvent.StartRepair) =>
                BuildReason((!ctx.RepairScheduledWithTechnician, "Repair must be scheduled and a technician assigned")),

            (InsuranceCaseStatus.RepairInProgress, CaseTriggerEvent.CompleteRepair) =>
                BuildReason(
                    (!ctx.RepairCompletionDateSet, "Repair completion date must be set"),
                    (ctx.CompletionPhotoCount < 1, "At least 1 completion photo is required"),
                    (!ctx.IntermediateInspectionDoneIfRequired, "Intermediate inspection must be done if required")),

            (InsuranceCaseStatus.RepairCompleted, CaseTriggerEvent.IssueSettlement) =>
                BuildReason((!ctx.SettlementIssued, "Settlement must be issued (final quote reconciled)")),

            (InsuranceCaseStatus.Settlement, CaseTriggerEvent.ConfirmPayment) =>
                BuildReason((!ctx.PaymentsCoverAgreedAmount, "Payment(s) must cover the agreed amount")),

            (InsuranceCaseStatus.PaymentConfirmed, CaseTriggerEvent.CloseCase) =>
                BuildReason((!ctx.RequiredDocumentsSentToInsurance, "Required documents must be sent to insurance")),

            (_, CaseTriggerEvent.Cancel) =>
                BuildReason((!ctx.ActorIsAdminOrBranchManager, "Only Admin or BranchManager may cancel a case")),

            _ => $"No transition '{trigger}' exists from '{_machine.State}'."
        };
    }

    private static string BuildReason(params (bool failing, string message)[] checks)
    {
        var failures = checks.Where(c => c.failing).Select(c => c.message).ToArray();
        return failures.Length == 0
            ? "Unknown reason — all known guards pass."
            : "Blocked: " + string.Join("; ", failures) + ".";
    }

    private void Configure()
    {
        _machine.Configure(InsuranceCaseStatus.NewCase)
            .PermitIf(CaseTriggerEvent.BookAssessorAppointment, InsuranceCaseStatus.AssessorAppointment,
                () => { var c = _contextProvider(); return c.HasAssessorAssigned && c.HasAccidentDate && c.IntakePhotoCount >= 1; })
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.AssessorAppointment)
            .PermitIf(CaseTriggerEvent.CompleteAssessment, InsuranceCaseStatus.Assessment,
                () => { var c = _contextProvider(); return c.HasAssessmentCompleted && c.WorkItemCount >= 1; })
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.Assessment)
            .PermitIf(CaseTriggerEvent.SubmitForInsuranceApproval, InsuranceCaseStatus.InsuranceApproval,
                () => { var c = _contextProvider(); return c.HasCurrentQuote && c.HasRequiredDocumentsForApprovalSubmission; })
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.InsuranceApproval)
            .PermitIf(CaseTriggerEvent.ApprovalReceived, InsuranceCaseStatus.CustomerAssignment,
                () => { var c = _contextProvider(); return c.HasApproval && c.ApprovalIsPositive; })
            .PermitIf(CaseTriggerEvent.ApprovalRejected, InsuranceCaseStatus.NewCase,
                () => _contextProvider().ApprovalIsRejected)
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.CustomerAssignment)
            .PermitIf(CaseTriggerEvent.CustomerAccepts, InsuranceCaseStatus.PartsApprovalAndOrder,
                () => _contextProvider().CustomerParticipationConfirmed)
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.PartsApprovalAndOrder)
            .PermitIf(CaseTriggerEvent.AllPartsReceived, InsuranceCaseStatus.RepairScheduling,
                () => _contextProvider().AllPartsReceivedOrNotNeeded)
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.RepairScheduling)
            .PermitIf(CaseTriggerEvent.StartRepair, InsuranceCaseStatus.RepairInProgress,
                () => _contextProvider().RepairScheduledWithTechnician)
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.RepairInProgress)
            .PermitIf(CaseTriggerEvent.CompleteRepair, InsuranceCaseStatus.RepairCompleted,
                () => { var c = _contextProvider();
                        return c.RepairCompletionDateSet
                            && c.CompletionPhotoCount >= 1
                            && c.IntermediateInspectionDoneIfRequired; })
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.RepairCompleted)
            .PermitIf(CaseTriggerEvent.IssueSettlement, InsuranceCaseStatus.Settlement,
                () => _contextProvider().SettlementIssued)
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.Settlement)
            .PermitIf(CaseTriggerEvent.ConfirmPayment, InsuranceCaseStatus.PaymentConfirmed,
                () => _contextProvider().PaymentsCoverAgreedAmount)
            .PermitIf(CaseTriggerEvent.Cancel, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().ActorIsAdminOrBranchManager);

        _machine.Configure(InsuranceCaseStatus.PaymentConfirmed)
            .PermitIf(CaseTriggerEvent.CloseCase, InsuranceCaseStatus.CaseClosed,
                () => _contextProvider().RequiredDocumentsSentToInsurance);

        _machine.Configure(InsuranceCaseStatus.CaseClosed);
    }
}
