using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Payments;
using Workshop.Application.Features.RetailCases;
using Workshop.Application.Features.RetailRepairs;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class RetailEndToEndPathTests
{
    [Fact]
    public async Task Walks_Quoted_To_Closed_WithEventTrail()
    {
        await using var db = TestDb.NewContext();
        var (custId, vehId, branchId) = await RetailCaseHandlersTests.SeedCustomerVehicleBranchAsync(db);
        var user = new TestCurrentUser();

        // 1. Create (Quoted)
        var caseId = await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                custId, vehId, branchId, null, "Φανοποιεία πλαϊνού", 400m, 96m,
                DateOnly.FromDateTime(DateTime.Today.AddDays(2)), null)), default);

        var transitions = new TransitionRetailCaseHandler(db, user, TimeProvider.System);

        // 2. Accept
        await transitions.Handle(
            new TransitionRetailCaseCommand(caseId, RetailCaseStatus.Accepted, "Customer ok"), default);

        // 3. Schedule + start + complete repair so we can move to InProgress and Completed.
        var techId = Guid.NewGuid();
        await new UpsertRetailRepairScheduleHandler(db).Handle(
            new UpsertRetailRepairScheduleCommand(caseId, new UpsertRetailRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today), null, techId)), default);
        await transitions.Handle(
            new TransitionRetailCaseCommand(caseId, RetailCaseStatus.InProgress, null), default);

        await new StartRetailRepairHandler(db, TimeProvider.System).Handle(
            new StartRetailRepairCommand(caseId), default);
        await new CompleteRetailRepairHandler(db).Handle(
            new CompleteRetailRepairCommand(caseId,
                new CompleteRetailRepairDto(DateTime.UtcNow)), default);

        await transitions.Handle(
            new TransitionRetailCaseCommand(caseId, RetailCaseStatus.Completed, null), default);

        // 4. Pay full amount, then move to Paid.
        await new CreateRetailPaymentHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(
            new CreateRetailPaymentCommand(caseId, new CreatePaymentDto(
                496m, DateOnly.FromDateTime(DateTime.Today), PaymentMethod.Cash,
                "Customer", null, null)), default);
        var summary = await new GetRetailSettlementSummaryHandler(db).Handle(
            new GetRetailSettlementSummaryQuery(caseId), default);
        Assert.True(summary.IsFullyPaid);

        await transitions.Handle(
            new TransitionRetailCaseCommand(caseId, RetailCaseStatus.Paid, null), default);

        // 5. Close.
        await transitions.Handle(
            new TransitionRetailCaseCommand(caseId, RetailCaseStatus.Closed, null), default);

        var finalState = await db.RetailCases.AsNoTracking().FirstAsync(c => c.Id == caseId);
        Assert.Equal(RetailCaseStatus.Closed, finalState.Status);

        // CaseEvents trail: Accepted, InProgress, Completed, Paid, Closed → 5 events.
        var events = await db.CaseEvents.AsNoTracking()
            .Where(e => e.RetailCaseId == caseId)
            .OrderBy(e => e.OccurredAt).ToListAsync();
        Assert.Equal(5, events.Count);
        Assert.Equal("Closed", events.Last().ToStatus);
    }

    [Fact]
    public async Task PaidTransition_BlockedWhenPaymentsBelowTotal()
    {
        await using var db = TestDb.NewContext();
        var (custId, vehId, branchId) = await RetailCaseHandlersTests.SeedCustomerVehicleBranchAsync(db);
        var user = new TestCurrentUser();
        var caseId = await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                custId, vehId, branchId, null, "Polish", 200m, 48m, null, null)), default);

        // Walk Quoted → Accepted → InProgress (with schedule) → Completed.
        var t = new TransitionRetailCaseHandler(db, user, TimeProvider.System);
        await t.Handle(new TransitionRetailCaseCommand(caseId, RetailCaseStatus.Accepted, null), default);
        await new UpsertRetailRepairScheduleHandler(db).Handle(
            new UpsertRetailRepairScheduleCommand(caseId, new UpsertRetailRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today), null, Guid.NewGuid())), default);
        await t.Handle(new TransitionRetailCaseCommand(caseId, RetailCaseStatus.InProgress, null), default);
        await new StartRetailRepairHandler(db, TimeProvider.System).Handle(
            new StartRetailRepairCommand(caseId), default);
        await new CompleteRetailRepairHandler(db).Handle(
            new CompleteRetailRepairCommand(caseId, new CompleteRetailRepairDto(DateTime.UtcNow)), default);
        await t.Handle(new TransitionRetailCaseCommand(caseId, RetailCaseStatus.Completed, null), default);

        // Pay only part of it.
        await new CreateRetailPaymentHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(
            new CreateRetailPaymentCommand(caseId, new CreatePaymentDto(
                100m, DateOnly.FromDateTime(DateTime.Today), PaymentMethod.Cash,
                "Customer", null, null)), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            t.Handle(new TransitionRetailCaseCommand(caseId, RetailCaseStatus.Paid, null), default));
        Assert.Contains("cover the case total", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
