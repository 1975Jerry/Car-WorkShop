using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Assessments;
using Workshop.Application.Features.InsuranceParts;
using Workshop.Application.Features.RetailCases;
using Workshop.Application.Features.RetailParts;
using Workshop.Application.Features.SupplierPortal;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class SupplierPortalHandlersTests
{
    private static async Task<(Guid supplierA, Guid supplierB, Guid insLineA, Guid retLineA)>
        SeedTwoSuppliersAsync(Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var supplierA = new Supplier { Name = "BodyParts Hellas", IsActive = true };
        var supplierB = new Supplier { Name = "RivalSupply", IsActive = true };
        db.Suppliers.AddRange(supplierA, supplierB);
        await db.SaveChangesAsync();

        // Insurance: seed an insurance case + assessment + part line owned by supplier A.
        var (insuranceCaseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var branchId = db.InsuranceCases.AsNoTracking().First(c => c.Id == insuranceCaseId).BranchId;

        await new UpsertAssessmentHandler(db, new AllowedOpsValidator(db)).Handle(
            new UpsertAssessmentCommand(insuranceCaseId,
                new AssessmentUpsertDto(
                    DateOnly.FromDateTime(DateTime.Today), false, null, null, 0m,
                    DateOnly.FromDateTime(DateTime.Today), false, null,
                    new[] { new WorkItemUpsertDto(
                        null, panelAllowed, "x", 50m, null, null, null, null, null, null, null, null, null, null) })),
            default);

        var insLineA = await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(insuranceCaseId,
                new InsurancePartLineUpsertDto(
                    supplierA.Id, branchId, PartType.Original,
                    "Bumper", 1m, 200m, null,
                    AvailabilityStatus.Available, false, null)), default);

        // Retail: seed a retail case + part line owned by supplier A.
        var (custId, vehId, branchId2) = await RetailCaseHandlersTests.SeedCustomerVehicleBranchAsync(db);
        var retailCaseId = await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                custId, vehId, branchId2, null, "Polish", 100m, 24m, null, null)), default);
        var retLineA = await new CreateRetailPartLineHandler(db).Handle(
            new CreateRetailPartLineCommand(retailCaseId, new RetailPartLineUpsertDto(
                supplierA.Id, branchId2, PartType.Original, "Mirror", 1m, 80m, null)), default);

        // Add an unrelated line under supplier B so we can prove scoping.
        await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(insuranceCaseId,
                new InsurancePartLineUpsertDto(
                    supplierB.Id, branchId, PartType.NonOEM,
                    "B-only part", 1m, 50m, null,
                    AvailabilityStatus.Available, false, null)), default);

        return (supplierA.Id, supplierB.Id, insLineA, retLineA);
    }

    [Fact]
    public async Task List_ScopesToOwnSupplier_AcrossBothAggregates()
    {
        await using var db = TestDb.NewContext();
        var (supA, supB, _, _) = await SeedTwoSuppliersAsync(db);

        var aRows = await new ListSupplierOrdersHandler(db).Handle(
            new ListSupplierOrdersQuery(supA), default);
        Assert.Equal(2, aRows.Count); // 1 insurance + 1 retail
        Assert.Contains(aRows, r => r.Kind == SupplierLineKind.Insurance);
        Assert.Contains(aRows, r => r.Kind == SupplierLineKind.Retail);

        var bRows = await new ListSupplierOrdersHandler(db).Handle(
            new ListSupplierOrdersQuery(supB), default);
        var bRow = Assert.Single(bRows);
        Assert.Equal("B-only part", bRow.PartName);
    }

    [Fact]
    public async Task List_StatusFilter()
    {
        await using var db = TestDb.NewContext();
        var (supA, _, insLine, _) = await SeedTwoSuppliersAsync(db);

        // Move one to Ordered, leave the retail line Pending.
        await new SupplierDispatchHandler(db, TimeProvider.System).Handle(
            new SupplierDispatchCommand(supA, insLine, SupplierLineKind.Insurance, PartReceivedStatus.Ordered),
            default);

        var ordered = await new ListSupplierOrdersHandler(db).Handle(
            new ListSupplierOrdersQuery(supA, PartReceivedStatus.Ordered), default);
        Assert.Single(ordered);
        var pending = await new ListSupplierOrdersHandler(db).Handle(
            new ListSupplierOrdersQuery(supA, PartReceivedStatus.Pending), default);
        Assert.Single(pending);
    }

    [Fact]
    public async Task Dispatch_HappyPath_InsuranceLine()
    {
        await using var db = TestDb.NewContext();
        var (supA, _, insLine, _) = await SeedTwoSuppliersAsync(db);

        var h = new SupplierDispatchHandler(db, TimeProvider.System);
        await h.Handle(new SupplierDispatchCommand(supA, insLine, SupplierLineKind.Insurance,
            PartReceivedStatus.Ordered), default);
        await h.Handle(new SupplierDispatchCommand(supA, insLine, SupplierLineKind.Insurance,
            PartReceivedStatus.InTransit, "ETA 3 days"), default);

        var line = await db.InsurancePartLines.AsNoTracking().FirstAsync(p => p.Id == insLine);
        Assert.Equal(PartReceivedStatus.InTransit, line.ReceivedStatus);
        Assert.True(line.Ordered);
        Assert.NotNull(line.OrderDate);
        Assert.Equal("ETA 3 days", line.Notes);
    }

    [Fact]
    public async Task Dispatch_HappyPath_RetailLine()
    {
        await using var db = TestDb.NewContext();
        var (supA, _, _, retLine) = await SeedTwoSuppliersAsync(db);

        await new SupplierDispatchHandler(db, TimeProvider.System).Handle(
            new SupplierDispatchCommand(supA, retLine, SupplierLineKind.Retail,
                PartReceivedStatus.Ordered), default);

        var line = await db.RetailPartLines.AsNoTracking().FirstAsync(p => p.Id == retLine);
        Assert.Equal(PartReceivedStatus.Ordered, line.ReceivedStatus);
    }

    [Fact]
    public async Task Dispatch_RefusesReceived()
    {
        await using var db = TestDb.NewContext();
        var (supA, _, insLine, _) = await SeedTwoSuppliersAsync(db);

        // Move to Ordered first so that Pending→Received transition rule wouldn't be the failure trigger.
        await new SupplierDispatchHandler(db, TimeProvider.System).Handle(
            new SupplierDispatchCommand(supA, insLine, SupplierLineKind.Insurance,
                PartReceivedStatus.Ordered), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SupplierDispatchHandler(db, TimeProvider.System).Handle(
                new SupplierDispatchCommand(supA, insLine, SupplierLineKind.Insurance,
                    PartReceivedStatus.Received), default));
        Assert.Contains("workshop", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_RefusesCrossSupplier()
    {
        await using var db = TestDb.NewContext();
        var (_, supB, insLine, _) = await SeedTwoSuppliersAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new SupplierDispatchHandler(db, TimeProvider.System).Handle(
                new SupplierDispatchCommand(supB, insLine, SupplierLineKind.Insurance,
                    PartReceivedStatus.Ordered), default));
    }

    [Fact]
    public async Task Validator_RejectsReceivedTarget()
    {
        var v = new SupplierDispatchValidator();
        var bad = new SupplierDispatchCommand(Guid.NewGuid(), Guid.NewGuid(),
            SupplierLineKind.Insurance, PartReceivedStatus.Received);
        Assert.False((await v.ValidateAsync(bad)).IsValid);
    }
}
