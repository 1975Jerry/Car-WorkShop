using Workshop.Domain.Enums;
using Workshop.Domain.Workflows;

namespace Workshop.Domain.Tests;

public class InsuranceCaseStateMachineTests
{
    private static CaseGuardContext PassingContext() => new()
    {
        HasAssessorAssigned = true,
        HasAccidentDate = true,
        IntakePhotoCount = 2,
        HasAssessmentCompleted = true,
        WorkItemCount = 3,
        HasCurrentQuote = true,
        HasRequiredDocumentsForApprovalSubmission = true,
        HasApproval = true,
        ApprovalIsPositive = true,
        ApprovalIsRejected = false,
        CustomerParticipationConfirmed = true,
        AllPartsReceivedOrNotNeeded = true,
        RepairScheduledWithTechnician = true,
        RepairCompletionDateSet = true,
        CompletionPhotoCount = 2,
        IntermediateInspectionDoneIfRequired = true,
        SettlementIssued = true,
        PaymentsCoverAgreedAmount = true,
        RequiredDocumentsSentToInsurance = true,
        ActorIsAdminOrBranchManager = true
    };

    private static InsuranceCaseStateMachine Machine(InsuranceCaseStatus start, CaseGuardContext ctx) =>
        new(start, () => ctx);

    [Fact]
    public void HappyPath_TraversesAllStates_FromNewCase_ToCaseClosed()
    {
        var ctx = PassingContext();
        var sm = Machine(InsuranceCaseStatus.NewCase, ctx);

        sm.Fire(CaseTriggerEvent.BookAssessorAppointment);
        Assert.Equal(InsuranceCaseStatus.AssessorAppointment, sm.State);

        sm.Fire(CaseTriggerEvent.CompleteAssessment);
        Assert.Equal(InsuranceCaseStatus.Assessment, sm.State);

        sm.Fire(CaseTriggerEvent.SubmitForInsuranceApproval);
        Assert.Equal(InsuranceCaseStatus.InsuranceApproval, sm.State);

        sm.Fire(CaseTriggerEvent.ApprovalReceived);
        Assert.Equal(InsuranceCaseStatus.CustomerAssignment, sm.State);

        sm.Fire(CaseTriggerEvent.CustomerAccepts);
        Assert.Equal(InsuranceCaseStatus.PartsApprovalAndOrder, sm.State);

        sm.Fire(CaseTriggerEvent.AllPartsReceived);
        Assert.Equal(InsuranceCaseStatus.RepairScheduling, sm.State);

        sm.Fire(CaseTriggerEvent.StartRepair);
        Assert.Equal(InsuranceCaseStatus.RepairInProgress, sm.State);

        sm.Fire(CaseTriggerEvent.CompleteRepair);
        Assert.Equal(InsuranceCaseStatus.RepairCompleted, sm.State);

        sm.Fire(CaseTriggerEvent.IssueSettlement);
        Assert.Equal(InsuranceCaseStatus.Settlement, sm.State);

        sm.Fire(CaseTriggerEvent.ConfirmPayment);
        Assert.Equal(InsuranceCaseStatus.PaymentConfirmed, sm.State);

        sm.Fire(CaseTriggerEvent.CloseCase);
        Assert.Equal(InsuranceCaseStatus.CaseClosed, sm.State);
    }

    [Fact]
    public void BookAssessorAppointment_RejectedWhenAssessorNotAssigned()
    {
        var ctx = PassingContext() with { HasAssessorAssigned = false };
        var sm = Machine(InsuranceCaseStatus.NewCase, ctx);

        Assert.False(sm.CanFire(CaseTriggerEvent.BookAssessorAppointment));
        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.BookAssessorAppointment));
        Assert.Contains("Assessor must be assigned", ex.Message);
    }

    [Fact]
    public void BookAssessorAppointment_RejectedWhenNoIntakePhotos()
    {
        var ctx = PassingContext() with { IntakePhotoCount = 0 };
        var sm = Machine(InsuranceCaseStatus.NewCase, ctx);

        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.BookAssessorAppointment));
        Assert.Contains("intake photo", ex.Message);
    }

    [Fact]
    public void CompleteAssessment_RejectedWhenNoWorkItems()
    {
        var ctx = PassingContext() with { WorkItemCount = 0 };
        var sm = Machine(InsuranceCaseStatus.AssessorAppointment, ctx);

        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.CompleteAssessment));
        Assert.Contains("WorkItem", ex.Message);
    }

    [Fact]
    public void SubmitForApproval_RejectedWhenNoQuote()
    {
        var ctx = PassingContext() with { HasCurrentQuote = false };
        var sm = Machine(InsuranceCaseStatus.Assessment, ctx);

        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.SubmitForInsuranceApproval));
        Assert.Contains("Quote", ex.Message);
    }

    [Fact]
    public void ApprovalRejected_ReturnsCaseToNewCase()
    {
        var ctx = PassingContext() with { ApprovalIsPositive = false, ApprovalIsRejected = true };
        var sm = Machine(InsuranceCaseStatus.InsuranceApproval, ctx);

        sm.Fire(CaseTriggerEvent.ApprovalRejected);
        Assert.Equal(InsuranceCaseStatus.NewCase, sm.State);
    }

    [Fact]
    public void AllPartsReceived_RejectedWhenPartsStillPending()
    {
        var ctx = PassingContext() with { AllPartsReceivedOrNotNeeded = false };
        var sm = Machine(InsuranceCaseStatus.PartsApprovalAndOrder, ctx);

        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.AllPartsReceived));
        Assert.Contains("parts must be Received", ex.Message);
    }

    [Fact]
    public void StartRepair_RejectedWithoutTechnician()
    {
        var ctx = PassingContext() with { RepairScheduledWithTechnician = false };
        var sm = Machine(InsuranceCaseStatus.RepairScheduling, ctx);

        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.StartRepair));
        Assert.Contains("technician", ex.Message);
    }

    [Fact]
    public void CompleteRepair_RejectedWithoutCompletionPhotos()
    {
        var ctx = PassingContext() with { CompletionPhotoCount = 0 };
        var sm = Machine(InsuranceCaseStatus.RepairInProgress, ctx);

        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.CompleteRepair));
        Assert.Contains("completion photo", ex.Message);
    }

    [Fact]
    public void ConfirmPayment_RejectedWhenPaymentInsufficient()
    {
        var ctx = PassingContext() with { PaymentsCoverAgreedAmount = false };
        var sm = Machine(InsuranceCaseStatus.Settlement, ctx);

        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.ConfirmPayment));
        Assert.Contains("cover the agreed amount", ex.Message);
    }

    [Fact]
    public void Cancel_RejectedForNonAdmin()
    {
        var ctx = PassingContext() with { ActorIsAdminOrBranchManager = false };
        var sm = Machine(InsuranceCaseStatus.RepairInProgress, ctx);

        Assert.False(sm.CanFire(CaseTriggerEvent.Cancel));
        var ex = Assert.Throws<InvalidOperationException>(() => sm.Fire(CaseTriggerEvent.Cancel));
        Assert.Contains("Admin or BranchManager", ex.Message);
    }

    [Fact]
    public void Cancel_AllowedForAdminFromAnyNonTerminalState()
    {
        var statesToTry = new[]
        {
            InsuranceCaseStatus.NewCase,
            InsuranceCaseStatus.AssessorAppointment,
            InsuranceCaseStatus.Assessment,
            InsuranceCaseStatus.InsuranceApproval,
            InsuranceCaseStatus.CustomerAssignment,
            InsuranceCaseStatus.PartsApprovalAndOrder,
            InsuranceCaseStatus.RepairScheduling,
            InsuranceCaseStatus.RepairInProgress,
            InsuranceCaseStatus.RepairCompleted,
            InsuranceCaseStatus.Settlement
        };

        foreach (var s in statesToTry)
        {
            var ctx = PassingContext();
            var sm = Machine(s, ctx);
            sm.Fire(CaseTriggerEvent.Cancel);
            Assert.True(sm.State == InsuranceCaseStatus.CaseClosed,
                userMessage: $"Cancel from {s} should land in CaseClosed but landed in {sm.State}");
        }
    }

    [Fact]
    public void CaseClosed_IsTerminal()
    {
        var sm = Machine(InsuranceCaseStatus.CaseClosed, PassingContext());

        foreach (var trigger in Enum.GetValues<CaseTriggerEvent>())
        {
            Assert.False(sm.CanFire(trigger),
                userMessage: $"No trigger should be permitted from CaseClosed, but {trigger} was");
        }
    }

    [Fact]
    public void WhyCannotFire_ReturnsHumanReadableBlockers()
    {
        var ctx = PassingContext() with
        {
            HasAssessorAssigned = false,
            HasAccidentDate = false,
            IntakePhotoCount = 0
        };
        var sm = Machine(InsuranceCaseStatus.NewCase, ctx);

        var reason = sm.WhyCannotFire(CaseTriggerEvent.BookAssessorAppointment);
        Assert.Contains("Assessor", reason);
        Assert.Contains("Accident date", reason);
        Assert.Contains("intake photo", reason);
    }
}
