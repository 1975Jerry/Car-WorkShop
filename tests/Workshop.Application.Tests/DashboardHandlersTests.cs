using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Dashboard;
using Workshop.Application.Features.InsuranceApprovals;
using Workshop.Application.Features.InsuranceParts;
using Workshop.Application.Features.RetailCases;
using Workshop.Application.Features.RetailParts;
using Workshop.Application.Features.RetailRepairs;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class DashboardHandlersTests
{
    [Fact]
    public async Task Kpis_CountOpenCases_AndPendingParts()
    {
        await using var db = TestDb.NewContext();
        var (insuranceCaseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var (retailCaseId, _) = await SeedRetailWithPendingPartAsync(db);

        // Add an assessment + pending insurance part line to the insurance case.
        await new Workshop.Application.Features.Assessments.UpsertAssessmentHandler(
                db, new Workshop.Application.Features.Assessments.AllowedOpsValidator(db))
            .Handle(new Workshop.Application.Features.Assessments.UpsertAssessmentCommand(
                insuranceCaseId,
                new Workshop.Application.Features.Assessments.AssessmentUpsertDto(
                    DateOnly.FromDateTime(DateTime.Today), false, null, null, 0m,
                    DateOnly.FromDateTime(DateTime.Today), false, null,
                    new[] { new Workshop.Application.Features.Assessments.WorkItemUpsertDto(
                        null, panelAllowed, "x", 50m, null, null, null, null, null, null, null, null, null, null) })),
                default);
        await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(insuranceCaseId,
                new InsurancePartLineUpsertDto(
                    null, BranchOf(db, insuranceCaseId), PartType.Original,
                    "Bumper", 1m, 200m, null,
                    AvailabilityStatus.Available, false, null)), default);

        var kpis = await new GetDashboardKpisHandler(db, TimeProvider.System).Handle(
            new GetDashboardKpisQuery(), default);

        Assert.Equal(1, kpis.OpenInsuranceCases);
        Assert.Equal(1, kpis.OpenRetailCases);
        // 1 pending part on insurance side + 1 on retail side = 2.
        Assert.Equal(2, kpis.PendingParts);
    }

    [Fact]
    public async Task Kpis_RepairsScheduledTodayIncludesBothSides()
    {
        await using var db = TestDb.NewContext();
        var (insuranceCaseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        db.Repairs.Add(new Workshop.Domain.Entities.Insurance.Repair
        {
            InsuranceCaseId = insuranceCaseId,
            ScheduledDate = DateOnly.FromDateTime(DateTime.Today),
            Status = RepairStatus.Scheduled
        });
        var (retailCaseId, _) = await SeedRetailWithPendingPartAsync(db);
        db.RetailRepairs.Add(new Workshop.Domain.Entities.Retail.RetailRepair
        {
            RetailCaseId = retailCaseId,
            ScheduledDate = DateOnly.FromDateTime(DateTime.Today),
            Status = RepairStatus.Scheduled
        });
        await db.SaveChangesAsync();

        var kpis = await new GetDashboardKpisHandler(db, TimeProvider.System).Handle(
            new GetDashboardKpisQuery(), default);
        Assert.Equal(2, kpis.RepairsScheduledToday);
    }

    [Fact]
    public async Task SettlementPipeline_SumsApprovedMinusPaid_PlusRetailOpenTotals()
    {
        await using var db = TestDb.NewContext();
        var (insuranceCaseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await new UpsertInsuranceApprovalHandler(db).Handle(
            new UpsertInsuranceApprovalCommand(insuranceCaseId,
                new InsuranceApprovalUpsertDto(
                    LiabilityAccepted: true, CustomerParticipation: false,
                    ParticipationAmount: null, ApprovedAmount: 1000m,
                    ApprovalDate: DateOnly.FromDateTime(DateTime.Today),
                    ApprovalStatus: ApprovalStatus.Approved, Notes: null)), default);
        db.Payments.Add(new Workshop.Domain.Entities.Insurance.Payment
        {
            InsuranceCaseId = insuranceCaseId,
            Amount = 300m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = PaymentMethod.Cash
        });

        var (retailCaseId, _) = await SeedRetailWithPendingPartAsync(db);
        // Retail case TotalWithVat is 248m (200 + 48 from the seed).
        await db.SaveChangesAsync();

        var kpis = await new GetDashboardKpisHandler(db, TimeProvider.System).Handle(
            new GetDashboardKpisQuery(), default);
        // 1000 approved - 300 paid = 700, + 248 retail = 948.
        Assert.Equal(948m, kpis.SettlementPipelineValue);
    }

    [Fact]
    public async Task BranchBreakdown_GroupsCasesPerBranch()
    {
        await using var db = TestDb.NewContext();
        var (insuranceCaseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var (_, _) = await SeedRetailWithPendingPartAsync(db);

        var rows = await new GetBranchBreakdownHandler(db).Handle(new GetBranchBreakdownQuery(), default);

        Assert.NotEmpty(rows);
        Assert.Contains(rows, r => r.InsuranceCases > 0 || r.RetailCases > 0);
        Assert.Equal(1, rows.Sum(r => r.InsuranceCases));
        Assert.Equal(1, rows.Sum(r => r.RetailCases));
    }

    [Theory]
    [InlineData(0, "0-7")]
    [InlineData(7, "0-7")]
    [InlineData(8, "8-30")]
    [InlineData(30, "8-30")]
    [InlineData(31, "31-60")]
    [InlineData(60, "31-60")]
    [InlineData(61, "60+")]
    [InlineData(180, "60+")]
    public async Task AgingBuckets_ClassifiesCasesByAge(int daysOld, string bucket)
    {
        await using var db = TestDb.NewContext();
        var (insuranceCaseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        // Backdate CreatedAt to land in the target bucket.
        var c = await db.InsuranceCases.FirstAsync(x => x.Id == insuranceCaseId);
        c.CreatedAt = DateTime.UtcNow.AddDays(-daysOld);
        await db.SaveChangesAsync();

        var rows = await new GetAgingBucketsHandler(db, TimeProvider.System).Handle(
            new GetAgingBucketsQuery(), default);
        var row = rows.First(r => r.BucketLabel == bucket);
        Assert.Equal(1, row.InsuranceCases);
        Assert.Equal(0, rows.Where(r => r.BucketLabel != bucket).Sum(r => r.InsuranceCases));
    }

    [Fact]
    public async Task PartsVariance_ComputesPartsCostMinusApproved()
    {
        await using var db = TestDb.NewContext();
        var (insuranceCaseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        // Approve 500, parts cost 700 → variance +200.
        await new UpsertInsuranceApprovalHandler(db).Handle(
            new UpsertInsuranceApprovalCommand(insuranceCaseId,
                new InsuranceApprovalUpsertDto(
                    LiabilityAccepted: true, CustomerParticipation: false,
                    ParticipationAmount: null, ApprovedAmount: 500m,
                    ApprovalDate: DateOnly.FromDateTime(DateTime.Today),
                    ApprovalStatus: ApprovalStatus.Approved, Notes: null)), default);
        // Need an Assessment record before InsurancePartLine.Create — use the same seed pattern.
        await new Workshop.Application.Features.Assessments.UpsertAssessmentHandler(
                db, new Workshop.Application.Features.Assessments.AllowedOpsValidator(db))
            .Handle(new Workshop.Application.Features.Assessments.UpsertAssessmentCommand(
                insuranceCaseId,
                new Workshop.Application.Features.Assessments.AssessmentUpsertDto(
                    DateOnly.FromDateTime(DateTime.Today), false, null, null, 0m,
                    DateOnly.FromDateTime(DateTime.Today), false, null,
                    new[] { new Workshop.Application.Features.Assessments.WorkItemUpsertDto(
                        null, null, "x", 50m, null, null, null, null, null, null, null, null, null, null) })),
                default);
        await new CreateInsurancePartLineHandler(db).Handle(
            new CreateInsurancePartLineCommand(insuranceCaseId,
                new InsurancePartLineUpsertDto(
                    null, BranchOf(db, insuranceCaseId), PartType.Original,
                    "Bumper", 1m, 700m, null,
                    AvailabilityStatus.Available, false, null)), default);

        var rows = await new GetPartsVarianceHandler(db).Handle(new GetPartsVarianceQuery(), default);
        var row = Assert.Single(rows);
        Assert.Equal(500m, row.ApprovedAmount);
        Assert.Equal(700m, row.PartsCost);
        Assert.Equal(200m, row.Variance);
    }

    private static Guid BranchOf(Workshop.Infrastructure.Persistence.WorkshopDbContext db, Guid insuranceCaseId)
    {
        return db.InsuranceCases.AsNoTracking().First(c => c.Id == insuranceCaseId).BranchId;
    }

    private static async Task<(Guid caseId, Guid branchId)> SeedRetailWithPendingPartAsync(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var (custId, vehId, branchId) = await RetailCaseHandlersTests.SeedCustomerVehicleBranchAsync(db);
        var caseId = await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                custId, vehId, branchId, null, "Polish", 200m, 48m, null, null)), default);
        await new CreateRetailPartLineHandler(db).Handle(
            new CreateRetailPartLineCommand(caseId, new RetailPartLineUpsertDto(
                null, branchId, PartType.Original, "Mirror", 1m, 80m, null)), default);
        return (caseId, branchId);
    }
}
