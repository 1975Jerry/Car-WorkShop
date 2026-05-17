namespace Workshop.Domain.Workflows;

public record CaseGuardContext
{
    public bool HasAssessorAssigned { get; init; }
    public bool HasAccidentDate { get; init; }
    public int IntakePhotoCount { get; init; }
    public bool HasAssessmentCompleted { get; init; }
    public int WorkItemCount { get; init; }
    public bool HasCurrentQuote { get; init; }
    public bool HasRequiredDocumentsForApprovalSubmission { get; init; }
    public bool HasApproval { get; init; }
    public bool ApprovalIsPositive { get; init; }
    public bool ApprovalIsRejected { get; init; }
    public bool CustomerParticipationConfirmed { get; init; }
    public bool AllPartsReceivedOrNotNeeded { get; init; }
    public bool RepairScheduledWithTechnician { get; init; }
    public bool RepairCompletionDateSet { get; init; }
    public int CompletionPhotoCount { get; init; }
    public bool IntermediateInspectionDoneIfRequired { get; init; }
    public bool SettlementIssued { get; init; }
    public bool PaymentsCoverAgreedAmount { get; init; }
    public bool RequiredDocumentsSentToInsurance { get; init; }
    public bool ActorIsAdminOrBranchManager { get; init; }
}
