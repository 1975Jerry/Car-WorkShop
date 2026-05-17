using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Repairs;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class RepairHandlersTests
{
    [Fact]
    public async Task Upsert_CreatesScheduledRepair()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var techId = Guid.CreateVersion7();

        var handler = new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        var id = await handler.Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                new TimeOnly(9, 0),
                techId, "Bring keys")), default);

        var saved = await db.Repairs.AsNoTracking().FirstAsync(r => r.Id == id);
        Assert.Equal(RepairStatus.Scheduled, saved.Status);
        Assert.Equal(techId, saved.TechnicianId);
        Assert.Equal(new TimeOnly(9, 0), saved.ScheduledTime);
    }

    [Fact]
    public async Task Upsert_UpdatesExistingSchedule()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var handler = new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());

        var firstTech = Guid.CreateVersion7();
        var secondTech = Guid.CreateVersion7();
        await handler.Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                new TimeOnly(9, 0), firstTech, null)), default);
        await handler.Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                new TimeOnly(11, 0), secondTech, null)), default);

        var saved = await db.Repairs.AsNoTracking().FirstAsync(r => r.InsuranceCaseId == caseId);
        Assert.Equal(secondTech, saved.TechnicianId);
        Assert.Equal(new TimeOnly(11, 0), saved.ScheduledTime);
    }

    [Fact]
    public async Task Upsert_RefusesAfterCompletion()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var handler = new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await handler.Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                null, Guid.CreateVersion7(), null)), default);

        var repair = await db.Repairs.FirstAsync(r => r.InsuranceCaseId == caseId);
        repair.Status = RepairStatus.Completed;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpsertRepairScheduleCommand(caseId,
                new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                    null, null, null)), default));
    }

    [Fact]
    public async Task StartRepair_FailsWithoutTechnician()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                null, TechnicianId: null, null)), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StartRepairHandler(db, TimeProvider.System).Handle(
                new StartRepairCommand(caseId), default));
    }

    [Fact]
    public async Task StartRepair_TransitionsToInProgressAndStampsStartDate()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                null, Guid.CreateVersion7(), null)), default);

        await new StartRepairHandler(db, TimeProvider.System).Handle(
            new StartRepairCommand(caseId), default);

        var saved = await db.Repairs.AsNoTracking().FirstAsync(r => r.InsuranceCaseId == caseId);
        Assert.Equal(RepairStatus.InProgress, saved.Status);
        Assert.NotNull(saved.StartDate);
    }

    [Fact]
    public async Task CompleteRepair_FailsBeforeStart()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                null, Guid.CreateVersion7(), null)), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CompleteRepairHandler(db).Handle(new CompleteRepairCommand(caseId,
                new CompleteRepairDto(DateTime.UtcNow, null)), default));
    }

    [Fact]
    public async Task CompleteRepair_AfterStart_TransitionsToCompleted()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                null, Guid.CreateVersion7(), null)), default);
        await new StartRepairHandler(db, TimeProvider.System).Handle(
            new StartRepairCommand(caseId), default);

        await new CompleteRepairHandler(db).Handle(new CompleteRepairCommand(caseId,
            new CompleteRepairDto(DateTime.UtcNow, "Done")), default);

        var saved = await db.Repairs.AsNoTracking().FirstAsync(r => r.InsuranceCaseId == caseId);
        Assert.Equal(RepairStatus.Completed, saved.Status);
        Assert.NotNull(saved.CompletionDate);
    }

    [Fact]
    public async Task MarkIntermediateInspection_TogglesFlag()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await new UpsertRepairScheduleHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new UpsertRepairScheduleCommand(caseId,
            new UpsertRepairScheduleDto(DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                null, Guid.CreateVersion7(), null)), default);

        var handler = new MarkIntermediateInspectionHandler(db);
        await handler.Handle(new MarkIntermediateInspectionCommand(caseId, true), default);

        var saved = await db.Repairs.AsNoTracking().FirstAsync(r => r.InsuranceCaseId == caseId);
        Assert.True(saved.IntermediateInspectionDone);

        await handler.Handle(new MarkIntermediateInspectionCommand(caseId, false), default);
        saved = await db.Repairs.AsNoTracking().FirstAsync(r => r.InsuranceCaseId == caseId);
        Assert.False(saved.IntermediateInspectionDone);
    }

    [Fact]
    public async Task MarkIntermediateInspection_FailsWithoutRepair()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new MarkIntermediateInspectionHandler(db).Handle(
                new MarkIntermediateInspectionCommand(caseId, true), default));
    }
}
