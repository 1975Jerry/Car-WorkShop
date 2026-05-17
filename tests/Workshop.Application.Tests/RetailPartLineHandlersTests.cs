using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.RetailCases;
using Workshop.Application.Features.RetailParts;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class RetailPartLineHandlersTests
{
    private static async Task<(Guid caseId, Guid branchId)> SeedAsync(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var (custId, vehId, branchId) = await RetailCaseHandlersTests.SeedCustomerVehicleBranchAsync(db);
        var caseId = await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                custId, vehId, branchId, null, "Bumper paint", 200m, 48m, null, null)), default);
        return (caseId, branchId);
    }

    [Fact]
    public async Task Create_ComputesTotalAndDefaultsToPending()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId) = await SeedAsync(db);

        var id = await new CreateRetailPartLineHandler(db).Handle(new CreateRetailPartLineCommand(
            caseId, new RetailPartLineUpsertDto(
                SupplierId: null, DestinationBranchId: branchId,
                PartType: PartType.NonOEM, PartName: "Plastic clip",
                Quantity: 3m, UnitCost: 1.50m, Notes: null)), default);

        var saved = await db.RetailPartLines.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(4.50m, saved.Total);
        Assert.Equal(PartReceivedStatus.Pending, saved.ReceivedStatus);
    }

    [Fact]
    public async Task ReceivedStatus_FullHappyPath()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId) = await SeedAsync(db);
        var warehouse = new Warehouse { BranchId = branchId, Name = "Main shelves" };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var id = await new CreateRetailPartLineHandler(db).Handle(new CreateRetailPartLineCommand(
            caseId, new RetailPartLineUpsertDto(null, branchId, PartType.Original,
                "Bumper", 1m, 250m, null)), default);

        var update = new UpdateRetailPartReceivedStatusHandler(db, TimeProvider.System);
        await update.Handle(new UpdateRetailPartReceivedStatusCommand(id, PartReceivedStatus.Ordered), default);
        await update.Handle(new UpdateRetailPartReceivedStatusCommand(id, PartReceivedStatus.InTransit), default);
        await update.Handle(new UpdateRetailPartReceivedStatusCommand(id, PartReceivedStatus.Received,
            warehouse.Id, "Row A4"), default);

        var saved = await db.RetailPartLines.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(PartReceivedStatus.Received, saved.ReceivedStatus);
        Assert.Equal(warehouse.Id, saved.WarehouseId);
        Assert.Equal("Row A4", saved.StorageLocation);
    }

    [Fact]
    public async Task ReceivedStatus_Received_RequiresWarehouse()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId) = await SeedAsync(db);
        var id = await new CreateRetailPartLineHandler(db).Handle(new CreateRetailPartLineCommand(
            caseId, new RetailPartLineUpsertDto(null, branchId, PartType.Original,
                "Mirror", 1m, 80m, null)), default);

        await new UpdateRetailPartReceivedStatusHandler(db, TimeProvider.System)
            .Handle(new UpdateRetailPartReceivedStatusCommand(id, PartReceivedStatus.Ordered), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateRetailPartReceivedStatusHandler(db, TimeProvider.System)
                .Handle(new UpdateRetailPartReceivedStatusCommand(id, PartReceivedStatus.Received), default));
    }

    [Fact]
    public async Task Delete_RefusesIfReceived_OkOtherwise()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId) = await SeedAsync(db);
        var warehouse = new Warehouse { BranchId = branchId, Name = "Main" };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var id = await new CreateRetailPartLineHandler(db).Handle(new CreateRetailPartLineCommand(
            caseId, new RetailPartLineUpsertDto(null, branchId, PartType.Original,
                "Mirror", 1m, 80m, null)), default);

        // Pending → can delete.
        var pending = new CreateRetailPartLineHandler(db);
        var deletableId = await pending.Handle(new CreateRetailPartLineCommand(caseId,
            new RetailPartLineUpsertDto(null, branchId, PartType.NonOEM, "Trash bag", 1m, 1m, null)), default);
        await new DeleteRetailPartLineHandler(db).Handle(new DeleteRetailPartLineCommand(deletableId), default);
        var deleted = await db.RetailPartLines.IgnoreQueryFilters().FirstAsync(p => p.Id == deletableId);
        Assert.True(deleted.IsDeleted);

        // Walk to Received → can't delete.
        var upd = new UpdateRetailPartReceivedStatusHandler(db, TimeProvider.System);
        await upd.Handle(new UpdateRetailPartReceivedStatusCommand(id, PartReceivedStatus.Ordered), default);
        await upd.Handle(new UpdateRetailPartReceivedStatusCommand(id, PartReceivedStatus.Received, warehouse.Id), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DeleteRetailPartLineHandler(db).Handle(new DeleteRetailPartLineCommand(id), default));
    }

    [Fact]
    public async Task Query_ReturnsLinesWithBranchAndWarehouseNames()
    {
        await using var db = TestDb.NewContext();
        var (caseId, branchId) = await SeedAsync(db);
        var warehouse = new Warehouse { BranchId = branchId, Name = "Main" };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var id = await new CreateRetailPartLineHandler(db).Handle(new CreateRetailPartLineCommand(
            caseId, new RetailPartLineUpsertDto(null, branchId, PartType.Original,
                "Door", 1m, 300m, null)), default);

        var rows = await new GetPartLinesForRetailCaseHandler(db)
            .Handle(new GetPartLinesForRetailCaseQuery(caseId), default);
        var row = Assert.Single(rows);
        Assert.Equal(id, row.Id);
        Assert.Equal("Athens", row.DestinationBranchName);
    }
}
