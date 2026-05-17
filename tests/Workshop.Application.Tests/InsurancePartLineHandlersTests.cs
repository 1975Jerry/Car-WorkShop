using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Assessments;
using Workshop.Application.Features.InsuranceParts;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class InsurancePartLineHandlersTests
{
    private static async Task<(Guid caseId, Guid branchId, Guid warehouseId, Guid supplierId)>
        SeedAsync(Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        // Need an Assessment record for part lines to attach.
        await new UpsertAssessmentHandler(db, new AllowedOpsValidator(db)).Handle(
            new UpsertAssessmentCommand(caseId, new AssessmentUpsertDto(
                DateOnly.FromDateTime(DateTime.Today), false, null, null, 100m,
                DateOnly.FromDateTime(DateTime.Today), false, null,
                new[] { new WorkItemUpsertDto(null, panelAllowed, "x",
                    50m, null, null, null, null, null, null, null, null, null, null) })),
            default);

        var insuranceCase = await db.InsuranceCases.AsNoTracking().FirstAsync(c => c.Id == caseId);
        var branchId = insuranceCase.BranchId;

        var warehouse = new Warehouse { BranchId = branchId, Name = "Main warehouse" };
        db.Warehouses.Add(warehouse);

        var supplier = new Supplier { Name = "Acme Parts", IsActive = true };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return (caseId, branchId, warehouse.Id, supplier.Id);
    }

    private static InsurancePartLineUpsertDto NewDto(Guid branchId, Guid? supplierId = null,
        string name = "Front bumper", decimal qty = 1m, decimal price = 200m) =>
        new(supplierId, branchId, PartType.Original, name, qty, price, null,
            AvailabilityStatus.Available, InsuranceApproved: false, Notes: null);

    [Fact]
    public async Task Create_AttachesToAssessmentAndComputesTotal()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId, _, supplierId) = await SeedAsync(db);

        var handler = new CreateInsurancePartLineHandler(db);
        var id = await handler.Handle(new CreateInsurancePartLineCommand(caseId,
            NewDto(branchId, supplierId, qty: 2m, price: 150m)), default);

        var saved = await db.InsurancePartLines.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(300m, saved.Total);
        Assert.Equal(PartReceivedStatus.Pending, saved.ReceivedStatus);
        Assert.False(saved.Ordered);
    }

    [Fact]
    public async Task Create_FailsWithoutAssessment()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var insuranceCase = await db.InsuranceCases.AsNoTracking().FirstAsync(c => c.Id == caseId);

        var handler = new CreateInsurancePartLineHandler(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateInsurancePartLineCommand(caseId,
                NewDto(insuranceCase.BranchId)), default));
    }

    [Fact]
    public async Task Update_RecomputesTotal()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId, _, supplierId) = await SeedAsync(db);

        var id = await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(caseId, NewDto(branchId, supplierId, qty: 1, price: 100)), default);

        await new UpdateInsurancePartLineHandler(db).Handle(
            new UpdateInsurancePartLineCommand(id, NewDto(branchId, supplierId, qty: 4, price: 100)), default);

        var saved = await db.InsurancePartLines.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(400m, saved.Total);
    }

    [Fact]
    public async Task Delete_RefusesWhenAlreadyReceived()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId, warehouseId, _) = await SeedAsync(db);

        var id = await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(caseId, NewDto(branchId)), default);

        var status = new UpdatePartReceivedStatusHandler(db, TimeProvider.System);
        await status.Handle(new UpdatePartReceivedStatusCommand(id, PartReceivedStatus.Ordered), default);
        await status.Handle(new UpdatePartReceivedStatusCommand(id, PartReceivedStatus.Received, warehouseId), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DeleteInsurancePartLineHandler(db).Handle(new DeleteInsurancePartLineCommand(id), default));
    }

    [Theory]
    [InlineData(PartReceivedStatus.Pending, PartReceivedStatus.Ordered, true)]
    [InlineData(PartReceivedStatus.Pending, PartReceivedStatus.InTransit, false)]
    [InlineData(PartReceivedStatus.Pending, PartReceivedStatus.Received, false)]
    [InlineData(PartReceivedStatus.Ordered, PartReceivedStatus.InTransit, true)]
    [InlineData(PartReceivedStatus.Ordered, PartReceivedStatus.Received, true)]
    [InlineData(PartReceivedStatus.InTransit, PartReceivedStatus.Received, true)]
    [InlineData(PartReceivedStatus.Received, PartReceivedStatus.Defective, true)]
    [InlineData(PartReceivedStatus.Received, PartReceivedStatus.Pending, false)]
    [InlineData(PartReceivedStatus.Cancelled, PartReceivedStatus.Pending, false)]
    [InlineData(PartReceivedStatus.Defective, PartReceivedStatus.Received, true)]
    public void TransitionMatrix_RejectsInvalidMoves(PartReceivedStatus from, PartReceivedStatus to, bool expected)
    {
        Assert.Equal(expected, UpdatePartReceivedStatusHandler.IsValidTransition(from, to));
    }

    [Fact]
    public async Task TransitionToReceived_RequiresWarehouse()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId, _, _) = await SeedAsync(db);

        var id = await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(caseId, NewDto(branchId)), default);

        var handler = new UpdatePartReceivedStatusHandler(db, TimeProvider.System);
        await handler.Handle(new UpdatePartReceivedStatusCommand(id, PartReceivedStatus.Ordered), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdatePartReceivedStatusCommand(id, PartReceivedStatus.Received), default));
    }

    [Fact]
    public async Task TransitionToReceived_SetsWarehouseStorageAndDate()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId, warehouseId, _) = await SeedAsync(db);

        var id = await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(caseId, NewDto(branchId)), default);

        var handler = new UpdatePartReceivedStatusHandler(db, TimeProvider.System);
        await handler.Handle(new UpdatePartReceivedStatusCommand(id, PartReceivedStatus.Ordered), default);
        await handler.Handle(new UpdatePartReceivedStatusCommand(
            id, PartReceivedStatus.Received, warehouseId, "Shelf-A4"), default);

        var saved = await db.InsurancePartLines.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(PartReceivedStatus.Received, saved.ReceivedStatus);
        Assert.Equal(warehouseId, saved.WarehouseId);
        Assert.Equal("Shelf-A4", saved.StorageLocation);
        Assert.NotNull(saved.ReceivedDate);
    }

    [Fact]
    public async Task TransitionToCancelled_FromAnyState_ClearsWarehouseAndOrderFlags()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId, warehouseId, _) = await SeedAsync(db);

        var id = await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(caseId, NewDto(branchId)), default);

        var handler = new UpdatePartReceivedStatusHandler(db, TimeProvider.System);
        await handler.Handle(new UpdatePartReceivedStatusCommand(id, PartReceivedStatus.Ordered), default);
        await handler.Handle(new UpdatePartReceivedStatusCommand(id, PartReceivedStatus.Cancelled), default);

        var saved = await db.InsurancePartLines.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(PartReceivedStatus.Cancelled, saved.ReceivedStatus);
        Assert.False(saved.Ordered);
        Assert.Null(saved.WarehouseId);
    }

    [Fact]
    public void AllowedNext_FromReceived_ProducesOnlyDefective()
    {
        var next = UpdatePartReceivedStatusHandler.AllowedNext(PartReceivedStatus.Received);
        Assert.Single(next);
        Assert.Equal(PartReceivedStatus.Defective, next[0]);
    }
}
