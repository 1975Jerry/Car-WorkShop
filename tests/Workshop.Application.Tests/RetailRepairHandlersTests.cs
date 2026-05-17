using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.RetailCases;
using Workshop.Application.Features.RetailRepairs;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class RetailRepairHandlersTests
{
    private static async Task<Guid> SeedCaseAsync(Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var (custId, vehId, branchId) = await RetailCaseHandlersTests.SeedCustomerVehicleBranchAsync(db);
        return await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                custId, vehId, branchId, null, "Polish", 100m, 24m, null, null)), default);
    }

    [Fact]
    public async Task Upsert_CreatesRepairWhenMissing_UpdatesWhenExists()
    {
        await using var db = TestDb.NewContext();
        var caseId = await SeedCaseAsync(db);

        var firstId = await new UpsertRetailRepairScheduleHandler(db).Handle(
            new UpsertRetailRepairScheduleCommand(caseId, new UpsertRetailRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today), new TimeOnly(9, 0), null)), default);

        var secondId = await new UpsertRetailRepairScheduleHandler(db).Handle(
            new UpsertRetailRepairScheduleCommand(caseId, new UpsertRetailRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today.AddDays(1)), new TimeOnly(11, 0), null)), default);

        Assert.Equal(firstId, secondId);
        var saved = await db.RetailRepairs.AsNoTracking().FirstAsync(r => r.Id == firstId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today.AddDays(1)), saved.ScheduledDate);
    }

    [Fact]
    public async Task Start_RequiresTechnician()
    {
        await using var db = TestDb.NewContext();
        var caseId = await SeedCaseAsync(db);
        await new UpsertRetailRepairScheduleHandler(db).Handle(
            new UpsertRetailRepairScheduleCommand(caseId, new UpsertRetailRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today), null, null)), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StartRetailRepairHandler(db, TimeProvider.System).Handle(
                new StartRetailRepairCommand(caseId), default));
    }

    [Fact]
    public async Task Complete_RequiresStart()
    {
        await using var db = TestDb.NewContext();
        var caseId = await SeedCaseAsync(db);
        await new UpsertRetailRepairScheduleHandler(db).Handle(
            new UpsertRetailRepairScheduleCommand(caseId, new UpsertRetailRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today), null, Guid.NewGuid())), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CompleteRetailRepairHandler(db).Handle(
                new CompleteRetailRepairCommand(caseId,
                    new CompleteRetailRepairDto(DateTime.UtcNow)), default));
    }

    [Fact]
    public async Task FullCycle_ScheduleStartComplete()
    {
        await using var db = TestDb.NewContext();
        var caseId = await SeedCaseAsync(db);
        var techId = Guid.NewGuid();

        await new UpsertRetailRepairScheduleHandler(db).Handle(
            new UpsertRetailRepairScheduleCommand(caseId, new UpsertRetailRepairScheduleDto(
                DateOnly.FromDateTime(DateTime.Today), null, techId)), default);
        await new StartRetailRepairHandler(db, TimeProvider.System).Handle(
            new StartRetailRepairCommand(caseId), default);
        await new CompleteRetailRepairHandler(db).Handle(
            new CompleteRetailRepairCommand(caseId,
                new CompleteRetailRepairDto(DateTime.UtcNow)), default);

        var saved = await db.RetailRepairs.AsNoTracking().FirstAsync(r => r.RetailCaseId == caseId);
        Assert.Equal(RepairStatus.Completed, saved.Status);
        Assert.NotNull(saved.StartDate);
        Assert.NotNull(saved.CompletionDate);
    }
}
