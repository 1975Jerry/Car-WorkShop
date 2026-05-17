using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Assessments;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class AssessmentHandlersTests
{
    internal static async Task<(Guid caseId, Guid panelAllowedId, Guid panelRestrictedId)>
        SeedCaseWithPanelsAsync(Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var branch = new Branch { Name = "Default", Code = "DEF", AddressLine = "x", City = "Athens", IsActive = true };
        var customer = new Customer
        {
            CustomerType = CustomerType.Individual,
            FirstName = "Test",
            LastName = "User",
            MobilePhone = "6900000000",
            GdprConsent = true,
            GdprConsentAt = DateTime.UtcNow,
            IsActive = true
        };
        var vehicle = new Vehicle
        {
            Customer = customer,
            PlateNumber = "ABC-1234",
            Brand = "Toyota",
            Model = "Yaris",
            IsActive = true
        };
        var insurer = new InsuranceCompany { Name = "Ergo", IsActive = true };

        // Panel A: full set; Panel B: only Polish allowed.
        var panelAllowed = new BodyPanel
        {
            Code = "P-ALL",
            DescriptionGr = "Allowed",
            Category = BodyPanelCategory.External,
            Side = PanelSide.Center,
            IsActive = true,
            AllowedOperations = new List<BodyPanelOperation>
            {
                new() { Operation = OperationType.Polish },
                new() { Operation = OperationType.Repair },
                new() { Operation = OperationType.Paint }
            }
        };
        var panelRestricted = new BodyPanel
        {
            Code = "P-RES",
            DescriptionGr = "Restricted",
            Category = BodyPanelCategory.External,
            Side = PanelSide.Center,
            IsActive = true,
            AllowedOperations = new List<BodyPanelOperation>
            {
                new() { Operation = OperationType.Polish }
            }
        };

        var insuranceCase = new InsuranceCase
        {
            CaseNumber = "INS-TEST-0001",
            Customer = customer,
            Vehicle = vehicle,
            Branch = branch,
            InsuranceCompany = insurer,
            Status = InsuranceCaseStatus.NewCase
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.Vehicles.Add(vehicle);
        db.InsuranceCompanies.Add(insurer);
        db.BodyPanels.Add(panelAllowed);
        db.BodyPanels.Add(panelRestricted);
        db.InsuranceCases.Add(insuranceCase);
        await db.SaveChangesAsync();

        return (insuranceCase.Id, panelAllowed.Id, panelRestricted.Id);
    }

    private static WorkItemUpsertDto WorkItem(Guid? panelId, decimal? polish = null, decimal? repair = null, decimal? paint = null) =>
        new(null, panelId, "Some description",
            Cost_Polish: polish,
            Cost_PDR: null,
            Cost_RemoveRefit: null,
            Cost_Replace: null,
            Cost_DisassembleAssemble: null,
            Cost_Repair: repair,
            Cost_Paint: paint,
            Cost_RepairPaint: null,
            Cost_Weld: null,
            Cost_Other: null,
            DiscountPct: null);

    [Fact]
    public async Task AllowedOpsValidator_AcceptsCostOnAllowedOperation()
    {
        await using var db = TestDb.NewContext();
        var (_, panelAllowed, _) = await SeedCaseWithPanelsAsync(db);

        var validator = new AllowedOpsValidator(db);
        var errors = await validator.ValidateAsync(
            new[] { WorkItem(panelAllowed, polish: 50, repair: 100) }, default);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task AllowedOpsValidator_RejectsCostOnDisallowedOperation()
    {
        await using var db = TestDb.NewContext();
        var (_, _, panelRestricted) = await SeedCaseWithPanelsAsync(db);

        var validator = new AllowedOpsValidator(db);
        var errors = await validator.ValidateAsync(
            new[] { WorkItem(panelRestricted, polish: 50, repair: 100) }, default);

        Assert.Single(errors);
        Assert.Contains("Repair", errors[0]);
        Assert.Contains("P-RES", errors[0]);
    }

    [Fact]
    public async Task AllowedOpsValidator_IgnoresZeroCostsOnDisallowedOps()
    {
        await using var db = TestDb.NewContext();
        var (_, _, panelRestricted) = await SeedCaseWithPanelsAsync(db);

        var validator = new AllowedOpsValidator(db);
        var errors = await validator.ValidateAsync(
            new[] { WorkItem(panelRestricted, polish: 50) }, default);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task AllowedOpsValidator_PanelIdNull_IsAllowed()
    {
        await using var db = TestDb.NewContext();
        await SeedCaseWithPanelsAsync(db);

        var validator = new AllowedOpsValidator(db);
        // Free-text rows (no BodyPanelId) must not be rejected by the matrix rule.
        var errors = await validator.ValidateAsync(
            new[] { WorkItem(null, polish: 10, repair: 20, paint: 30) }, default);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task UpsertAssessment_CreatesAssessment_WithComputedTotals()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await SeedCaseWithPanelsAsync(db);

        var handler = new UpsertAssessmentHandler(db, new AllowedOpsValidator(db));
        var dto = new AssessmentUpsertDto(
            AssessmentDate: DateOnly.FromDateTime(DateTime.Today),
            PartsRequired: true,
            PartsCost: 200m,
            PaintMaterialsCost: 50m,
            AgreedAmount: 500m,
            AgreementDate: DateOnly.FromDateTime(DateTime.Today),
            IntermediateInspection: false,
            Notes: null,
            WorkItems: new[] { WorkItem(panelAllowed, polish: 100, repair: 200) });

        var assessmentId = await handler.Handle(new UpsertAssessmentCommand(caseId, dto), default);

        var saved = await db.Assessments.AsNoTracking()
            .Include(a => a.WorkItems)
            .FirstAsync(a => a.Id == assessmentId);
        Assert.Equal(300m, saved.LaborCost);
        Assert.Equal(550m, saved.TotalEstimatedCost); // 300 labor + 200 parts + 50 paint
        Assert.Single(saved.WorkItems);
        Assert.Equal(300m, saved.WorkItems.First().Total);
    }

    [Fact]
    public async Task UpsertAssessment_RejectsCostOnDisallowedOp()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, panelRestricted) = await SeedCaseWithPanelsAsync(db);

        var handler = new UpsertAssessmentHandler(db, new AllowedOpsValidator(db));
        var dto = new AssessmentUpsertDto(
            AssessmentDate: DateOnly.FromDateTime(DateTime.Today),
            PartsRequired: false,
            PartsCost: null,
            PaintMaterialsCost: null,
            AgreedAmount: 100m,
            AgreementDate: DateOnly.FromDateTime(DateTime.Today),
            IntermediateInspection: false,
            Notes: null,
            WorkItems: new[] { WorkItem(panelRestricted, polish: 10, paint: 50) });

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new UpsertAssessmentCommand(caseId, dto), default));
    }

    [Fact]
    public async Task UpsertAssessment_UpdatesExistingAndRemovesDeletedRows()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await SeedCaseWithPanelsAsync(db);

        var handler = new UpsertAssessmentHandler(db, new AllowedOpsValidator(db));

        // Create with two work items.
        var first = new AssessmentUpsertDto(
            DateOnly.FromDateTime(DateTime.Today), false, null, null,
            300m, DateOnly.FromDateTime(DateTime.Today), false, null,
            new[] {
                WorkItem(panelAllowed, polish: 50),
                WorkItem(panelAllowed, repair: 100)
            });
        await handler.Handle(new UpsertAssessmentCommand(caseId, first), default);

        var saved = await db.Assessments.AsNoTracking()
            .Include(a => a.WorkItems).FirstAsync();
        Assert.Equal(2, saved.WorkItems.Count);
        Assert.Equal(150m, saved.LaborCost);

        var keptId = saved.WorkItems.First(w => w.Cost_Repair == 100m).Id;

        // Update: keep only the repair row, increase to 250.
        var second = new AssessmentUpsertDto(
            DateOnly.FromDateTime(DateTime.Today), false, null, null,
            300m, DateOnly.FromDateTime(DateTime.Today), false, null,
            new[] {
                new WorkItemUpsertDto(keptId, panelAllowed, "Updated",
                    null, null, null, null, null,
                    Cost_Repair: 250m, null, null, null, null, null)
            });
        await handler.Handle(new UpsertAssessmentCommand(caseId, second), default);

        var after = await db.Assessments.AsNoTracking()
            .Include(a => a.WorkItems).FirstAsync();
        Assert.Single(after.WorkItems);
        Assert.Equal(250m, after.LaborCost);
        Assert.Equal(keptId, after.WorkItems.First().Id);
    }
}
