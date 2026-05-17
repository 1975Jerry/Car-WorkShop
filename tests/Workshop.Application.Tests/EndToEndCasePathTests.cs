using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Assessments;
using Workshop.Application.Features.InsuranceApprovals;
using Workshop.Application.Features.InsuranceCases;
using Workshop.Application.Features.InsuranceParts;
using Workshop.Application.Features.Payments;
using Workshop.Application.Features.Quotes;
using Workshop.Application.Features.Repairs;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

/// <summary>
/// Phase 4 first end-to-end: NewCase → AssessorAppointment → Assessment →
/// InsuranceApproval → CustomerAssignment → PartsApprovalAndOrder.
/// Parts/Repair/etc. continue in later phases.
/// </summary>
public class EndToEndCasePathTests
{
    [Fact]
    public async Task FullPath_NewCase_To_PartsApprovalAndOrder()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        // Seed assessor on the case (required by BookAssessorAppointment guard).
        var assessor = new Assessor { FullName = "Assessor One", IsActive = true };
        db.Assessors.Add(assessor);
        var insuranceCase = await db.InsuranceCases.FirstAsync(c => c.Id == caseId);
        insuranceCase.AssessorId = assessor.Id;
        insuranceCase.AccidentDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        await db.SaveChangesAsync();

        var user = new TestCurrentUser();
        // Need a User row because CaseEvent.TriggeredById FKs to User — InMemory
        // doesn't enforce but we add for completeness.
        db.Users.Add(new Workshop.Domain.Entities.Identity.User
        {
            Id = user.UserId!.Value,
            FullName = "Test User",
            UserName = "test@x",
            Email = "test@x",
            PortalAudience = PortalAudience.Staff,
            Language = "el",
            IsActive = true
        });

        // Add an intake photo (the BookAssessorAppointment guard needs ≥1).
        var assessmentForPhoto = await db.Assessments.FirstOrDefaultAsync(a => a.InsuranceCaseId == caseId);
        // Photos belong to Assessment, but we haven't created one yet. So we create
        // a stub Assessment first, attach the intake photo to it, then keep it as the
        // assessment record we'll later complete.
        if (assessmentForPhoto is null)
        {
            assessmentForPhoto = new Assessment
            {
                InsuranceCaseId = caseId,
                AssessmentDate = DateOnly.FromDateTime(DateTime.Today)
            };
            db.Assessments.Add(assessmentForPhoto);
            await db.SaveChangesAsync();
        }
        db.Photos.Add(new Photo
        {
            AssessmentId = assessmentForPhoto.Id,
            Phase = PhotoPhase.Intake,
            FileName = "intake.jpg",
            FilePath = "/x",
            ContentType = "image/jpeg",
            SizeBytes = 1,
            UploadedById = user.UserId.Value
        });
        await db.SaveChangesAsync();

        // Seed CompanyProfile for VAT in IssueQuote.
        db.CompanyProfiles.Add(new CompanyProfile
        {
            Name = "Paint Bull", AddressLine = "x", City = "Athens",
            Phone = "x", VatNumber = "x", DefaultVatRate = 24m
        });
        await db.SaveChangesAsync();

        var guardBuilder = new CaseGuardContextBuilder(db, user);
        var transition = new TransitionInsuranceCaseHandler(db, guardBuilder, user, TimeProvider.System, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());

        // 1. NewCase → AssessorAppointment
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.BookAssessorAppointment, null), default);
        Assert.Equal(InsuranceCaseStatus.AssessorAppointment, await StatusOf(db, caseId));

        // 2. Complete Assessment via upsert (writes work items + totals + agreed amount).
        var assessmentHandler = new UpsertAssessmentHandler(db, new AllowedOpsValidator(db));
        var assessmentDto = new AssessmentUpsertDto(
            DateOnly.FromDateTime(DateTime.Today), false, null, null, 300m,
            DateOnly.FromDateTime(DateTime.Today), false, null,
            new[] {
                new WorkItemUpsertDto(null, panelAllowed, "Front bumper",
                    100m, null, null, null, null, 200m, null, null, null, null, null)
            });
        await assessmentHandler.Handle(new UpsertAssessmentCommand(caseId, assessmentDto), default);

        // 3. AssessorAppointment → Assessment
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.CompleteAssessment, null), default);
        Assert.Equal(InsuranceCaseStatus.Assessment, await StatusOf(db, caseId));

        // 4. Issue Quote (so HasCurrentQuote guard passes).
        await new IssueQuoteHandler(db, user, TimeProvider.System, new FakeQuotePdfGenerator(), new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients())
            .Handle(new IssueQuoteCommand(caseId), default);

        // 4b. Upload required documents (CaseForm + InsuranceForm) so the guard passes.
        SeedDocumentRow(db, caseId, DocumentType.CaseForm, user.UserId.Value);
        SeedDocumentRow(db, caseId, DocumentType.InsuranceForm, user.UserId.Value);
        await db.SaveChangesAsync();

        // 5. Assessment → InsuranceApproval
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.SubmitForInsuranceApproval, null), default);
        Assert.Equal(InsuranceCaseStatus.InsuranceApproval, await StatusOf(db, caseId));

        // 6. Record the approval with status = Approved.
        await new UpsertInsuranceApprovalHandler(db).Handle(
            new UpsertInsuranceApprovalCommand(caseId, new InsuranceApprovalUpsertDto(
                LiabilityAccepted: true,
                CustomerParticipation: false,
                ParticipationAmount: null,
                ApprovedAmount: 300m,
                ApprovalDate: DateOnly.FromDateTime(DateTime.Today),
                ApprovalStatus: ApprovalStatus.Approved,
                Notes: null)), default);

        // 7. InsuranceApproval → CustomerAssignment
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.ApprovalReceived, null), default);
        Assert.Equal(InsuranceCaseStatus.CustomerAssignment, await StatusOf(db, caseId));

        // 8. CustomerAssignment → PartsApprovalAndOrder
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.CustomerAccepts, null), default);
        Assert.Equal(InsuranceCaseStatus.PartsApprovalAndOrder, await StatusOf(db, caseId));

        // 9. Add two parts; mark one Received, one Cancelled. Both count as "not pending".
        var caseRow = await db.InsuranceCases.AsNoTracking().FirstAsync(c => c.Id == caseId);
        var warehouse = new Warehouse { BranchId = caseRow.BranchId, Name = "Main" };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var partsHandler = new CreateInsurancePartLineHandler(db);
        var partA = await partsHandler.Handle(new CreateInsurancePartLineCommand(caseId,
            new InsurancePartLineUpsertDto(null, caseRow.BranchId, PartType.Original,
                "Fender", 1m, 200m, null, AvailabilityStatus.Available, true, null)), default);
        var partB = await partsHandler.Handle(new CreateInsurancePartLineCommand(caseId,
            new InsurancePartLineUpsertDto(null, caseRow.BranchId, PartType.Original,
                "Headlight", 1m, 150m, null, AvailabilityStatus.OutOfStock, true, null)), default);

        var statusHandler = new UpdatePartReceivedStatusHandler(db, TimeProvider.System);
        await statusHandler.Handle(new UpdatePartReceivedStatusCommand(partA, PartReceivedStatus.Ordered), default);
        await statusHandler.Handle(new UpdatePartReceivedStatusCommand(partA, PartReceivedStatus.Received, warehouse.Id, "A-1"), default);
        await statusHandler.Handle(new UpdatePartReceivedStatusCommand(partB, PartReceivedStatus.Cancelled), default);

        // 10. PartsApprovalAndOrder → RepairScheduling
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.AllPartsReceived, null), default);
        Assert.Equal(InsuranceCaseStatus.RepairScheduling, await StatusOf(db, caseId));

        // 11. Schedule repair with technician.
        var techId = user.UserId!.Value; // reuse the test user as a technician
        await new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(
            new UpsertRepairScheduleCommand(caseId, new UpsertRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                new TimeOnly(9, 0), techId, "Phase-6 scheduled")), default);

        // 12. RepairScheduling → RepairInProgress
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.StartRepair, null), default);
        Assert.Equal(InsuranceCaseStatus.RepairInProgress, await StatusOf(db, caseId));

        await new StartRepairHandler(db, TimeProvider.System).Handle(
            new StartRepairCommand(caseId), default);
        await new CompleteRepairHandler(db).Handle(new CompleteRepairCommand(caseId,
            new CompleteRepairDto(DateTime.UtcNow, "Phase-6 completed")), default);

        // 12b. Upload at least one Completion photo so the guard passes.
        var repairForPhoto = await db.Repairs.FirstAsync(r => r.InsuranceCaseId == caseId);
        SeedPhotoRow(db, repairId: repairForPhoto.Id, PhotoPhase.Completion, user.UserId.Value);
        await db.SaveChangesAsync();

        // 13. RepairInProgress → RepairCompleted
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.CompleteRepair, null), default);
        Assert.Equal(InsuranceCaseStatus.RepairCompleted, await StatusOf(db, caseId));

        // 14. RepairCompleted → Settlement (guard: SettlementIssued = AgreedAmount > 0)
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.IssueSettlement, null), default);
        Assert.Equal(InsuranceCaseStatus.Settlement, await StatusOf(db, caseId));

        // 15. Record payments covering the approved amount (300m).
        await new CreatePaymentHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new CreatePaymentCommand(caseId,
            new CreatePaymentDto(300m, DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod.InsurancePayout, "Insurer", "REF-1", null)), default);

        // 16. Settlement → PaymentConfirmed
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.ConfirmPayment, null), default);
        Assert.Equal(InsuranceCaseStatus.PaymentConfirmed, await StatusOf(db, caseId));

        // 17. Mark one document as sent (closing guard requires ≥1).
        var caseFormDoc = await db.Documents.FirstAsync(d =>
            d.InsuranceCaseId == caseId && d.DocumentType == DocumentType.CaseForm);
        caseFormDoc.SentToInsurance = true;
        caseFormDoc.SentToInsuranceAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // 18. PaymentConfirmed → CaseClosed
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.CloseCase, null), default);
        Assert.Equal(InsuranceCaseStatus.CaseClosed, await StatusOf(db, caseId));

        // ClosedAt should be stamped by the transition handler.
        var closed = await db.InsuranceCases.AsNoTracking().FirstAsync(c => c.Id == caseId);
        Assert.NotNull(closed.ClosedAt);

        // CaseEvents should record every transition.
        var events = await db.CaseEvents.AsNoTracking()
            .Where(e => e.InsuranceCaseId == caseId)
            .OrderBy(e => e.OccurredAt).ToListAsync();
        Assert.Equal(11, events.Count);
    }

    [Fact]
    public async Task ConfirmPayment_BlockedWhenPaymentsBelowAgreedAmount()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var insuranceCase = await db.InsuranceCases.FirstAsync(c => c.Id == caseId);
        insuranceCase.Status = InsuranceCaseStatus.Settlement;
        await db.SaveChangesAsync();

        var user = new TestCurrentUser();
        db.Users.Add(new Workshop.Domain.Entities.Identity.User
        {
            Id = user.UserId!.Value, FullName = "T", UserName = "t@x", Email = "t@x",
            PortalAudience = PortalAudience.Staff, Language = "el", IsActive = true
        });
        await new UpsertInsuranceApprovalHandler(db).Handle(
            new UpsertInsuranceApprovalCommand(caseId, new InsuranceApprovalUpsertDto(
                true, false, null, 500m,
                DateOnly.FromDateTime(DateTime.Today), ApprovalStatus.Approved, null)), default);
        await new CreatePaymentHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new CreatePaymentCommand(caseId,
            new CreatePaymentDto(200m, DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod.Cash, null, null, null)), default);

        var transition = new TransitionInsuranceCaseHandler(db, new CaseGuardContextBuilder(db, user), user, TimeProvider.System, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transition.Handle(new TransitionInsuranceCaseCommand(caseId,
                CaseTriggerEvent.ConfirmPayment, null), default));

        // Top up the payment and try again — should succeed.
        await new CreatePaymentHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new CreatePaymentCommand(caseId,
            new CreatePaymentDto(300m, DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod.Cash, null, null, null)), default);
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.ConfirmPayment, null), default);
        Assert.Equal(InsuranceCaseStatus.PaymentConfirmed, await StatusOf(db, caseId));
    }

    [Fact]
    public async Task CloseCase_BlockedWithoutSentDocument()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var insuranceCase = await db.InsuranceCases.FirstAsync(c => c.Id == caseId);
        insuranceCase.Status = InsuranceCaseStatus.PaymentConfirmed;
        await db.SaveChangesAsync();

        var user = new TestCurrentUser();
        db.Users.Add(new Workshop.Domain.Entities.Identity.User
        {
            Id = user.UserId!.Value, FullName = "T", UserName = "t@x", Email = "t@x",
            PortalAudience = PortalAudience.Staff, Language = "el", IsActive = true
        });

        var transition = new TransitionInsuranceCaseHandler(db, new CaseGuardContextBuilder(db, user), user, TimeProvider.System, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transition.Handle(new TransitionInsuranceCaseCommand(caseId,
                CaseTriggerEvent.CloseCase, null), default));
    }

    [Fact]
    public async Task StartRepair_BlocksWithoutTechnician()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var insuranceCase = await db.InsuranceCases.FirstAsync(c => c.Id == caseId);
        insuranceCase.Status = InsuranceCaseStatus.RepairScheduling;
        await db.SaveChangesAsync();

        var user = new TestCurrentUser();
        db.Users.Add(new Workshop.Domain.Entities.Identity.User
        {
            Id = user.UserId!.Value, FullName = "T",
            UserName = "t@x", Email = "t@x",
            PortalAudience = PortalAudience.Staff, Language = "el", IsActive = true
        });
        // Schedule the repair but DO NOT assign a technician.
        await new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(
            new UpsertRepairScheduleCommand(caseId, new UpsertRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                null, null, null)), default);

        var transition = new TransitionInsuranceCaseHandler(db, new CaseGuardContextBuilder(db, user), user, TimeProvider.System, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transition.Handle(new TransitionInsuranceCaseCommand(caseId,
                CaseTriggerEvent.StartRepair, null), default));
    }

    [Fact]
    public async Task CompleteRepair_BlockedWhenIntermediateInspectionRequiredButNotDone()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        // Mark Assessment with IntermediateInspection=true
        await new UpsertAssessmentHandler(db, new AllowedOpsValidator(db)).Handle(
            new UpsertAssessmentCommand(caseId, new AssessmentUpsertDto(
                DateOnly.FromDateTime(DateTime.Today), false, null, null, 100m,
                DateOnly.FromDateTime(DateTime.Today), IntermediateInspection: true, null,
                new[] { new WorkItemUpsertDto(null, panelAllowed, "x",
                    50m, null, null, null, null, null, null, null, null, null, null) })),
            default);

        var insuranceCase = await db.InsuranceCases.FirstAsync(c => c.Id == caseId);
        insuranceCase.Status = InsuranceCaseStatus.RepairInProgress;
        await db.SaveChangesAsync();

        var user = new TestCurrentUser();
        db.Users.Add(new Workshop.Domain.Entities.Identity.User
        {
            Id = user.UserId!.Value, FullName = "T", UserName = "t@x", Email = "t@x",
            PortalAudience = PortalAudience.Staff, Language = "el", IsActive = true
        });

        await new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today),
                null, user.UserId, null)), default);
        await new StartRepairHandler(db, TimeProvider.System).Handle(new StartRepairCommand(caseId), default);
        await new CompleteRepairHandler(db).Handle(new CompleteRepairCommand(caseId,
            new CompleteRepairDto(DateTime.UtcNow, null)), default);

        var repairForPhoto = await db.Repairs.FirstAsync(r => r.InsuranceCaseId == caseId);
        SeedPhotoRow(db, repairForPhoto.Id, PhotoPhase.Completion, user.UserId!.Value);
        await db.SaveChangesAsync();

        // Intermediate inspection is required but NOT marked done — guard should block.
        var transition = new TransitionInsuranceCaseHandler(db, new CaseGuardContextBuilder(db, user), user, TimeProvider.System, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transition.Handle(new TransitionInsuranceCaseCommand(caseId,
                CaseTriggerEvent.CompleteRepair, null), default));

        // Now mark inspection done → should succeed.
        await new MarkIntermediateInspectionHandler(db).Handle(
            new MarkIntermediateInspectionCommand(caseId, true), default);
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.CompleteRepair, null), default);
        Assert.Equal(InsuranceCaseStatus.RepairCompleted, await StatusOf(db, caseId));
    }

    [Fact]
    public async Task PartsApproval_BlocksWhenAnyPartIsPending()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        var insuranceCase = await db.InsuranceCases.FirstAsync(c => c.Id == caseId);
        insuranceCase.Status = InsuranceCaseStatus.PartsApprovalAndOrder;
        await db.SaveChangesAsync();

        var user = new TestCurrentUser();
        db.Users.Add(new Workshop.Domain.Entities.Identity.User
        {
            Id = user.UserId!.Value, FullName = "T",
            UserName = "t@x", Email = "t@x",
            PortalAudience = PortalAudience.Staff, Language = "el", IsActive = true
        });

        await new UpsertAssessmentHandler(db, new AllowedOpsValidator(db)).Handle(
            new UpsertAssessmentCommand(caseId, new AssessmentUpsertDto(
                DateOnly.FromDateTime(DateTime.Today), false, null, null, 100m,
                DateOnly.FromDateTime(DateTime.Today), false, null,
                new[] { new WorkItemUpsertDto(null, panelAllowed, "x",
                    50m, null, null, null, null, null, null, null, null, null, null) })),
            default);

        // Add one Pending part — should block.
        await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(caseId,
                new InsurancePartLineUpsertDto(null, insuranceCase.BranchId, PartType.Original,
                    "Pending Part", 1m, 100m, null, AvailabilityStatus.Available, true, null)),
            default);

        var transition = new TransitionInsuranceCaseHandler(db, new CaseGuardContextBuilder(db, user), user, TimeProvider.System, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transition.Handle(new TransitionInsuranceCaseCommand(caseId,
                CaseTriggerEvent.AllPartsReceived, null), default));
    }

    [Fact]
    public async Task ApprovalRejected_SendsCaseBackToNewCase()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var insuranceCase = await db.InsuranceCases.FirstAsync(c => c.Id == caseId);
        insuranceCase.Status = InsuranceCaseStatus.InsuranceApproval;
        await db.SaveChangesAsync();

        var user = new TestCurrentUser();
        db.Users.Add(new Workshop.Domain.Entities.Identity.User
        {
            Id = user.UserId!.Value, FullName = "T",
            UserName = "t@x", Email = "t@x",
            PortalAudience = PortalAudience.Staff, Language = "el", IsActive = true
        });
        await new UpsertInsuranceApprovalHandler(db).Handle(
            new UpsertInsuranceApprovalCommand(caseId, new InsuranceApprovalUpsertDto(
                true, false, null, 0m,
                DateOnly.FromDateTime(DateTime.Today), ApprovalStatus.Rejected, "Not covered")), default);

        var transition = new TransitionInsuranceCaseHandler(db, new CaseGuardContextBuilder(db, user), user, TimeProvider.System, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await transition.Handle(new TransitionInsuranceCaseCommand(caseId,
            CaseTriggerEvent.ApprovalRejected, "Rejected"), default);

        Assert.Equal(InsuranceCaseStatus.NewCase, await StatusOf(db, caseId));
    }

    private static async Task<InsuranceCaseStatus> StatusOf(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db, Guid caseId) =>
        await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == caseId)
            .Select(c => c.Status).FirstAsync();

    internal static void SeedDocumentRow(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db,
        Guid insuranceCaseId, DocumentType type, Guid userId) =>
        db.Documents.Add(new Document
        {
            InsuranceCaseId = insuranceCaseId,
            DocumentType = type,
            FileName = $"{type}.pdf",
            FilePath = $"uploads/docs/{type}.pdf",
            ContentType = "application/pdf",
            SizeBytes = 100,
            UploadedById = userId
        });

    internal static void SeedPhotoRow(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db,
        Guid repairId, PhotoPhase phase, Guid userId) =>
        db.Photos.Add(new Photo
        {
            RepairId = repairId,
            Phase = phase,
            FileName = $"{phase}.jpg",
            FilePath = $"uploads/photos/{phase}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            UploadedById = userId
        });
}
