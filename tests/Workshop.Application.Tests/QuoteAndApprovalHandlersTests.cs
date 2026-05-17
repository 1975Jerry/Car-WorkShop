using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Assessments;
using Workshop.Application.Features.InsuranceApprovals;
using Workshop.Application.Features.Quotes;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class QuoteAndApprovalHandlersTests
{
    private static async Task<Guid> SeedAssessmentForCaseAsync(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db, Guid caseId, Guid panelId,
        decimal polish = 100m, decimal repair = 200m)
    {
        var handler = new UpsertAssessmentHandler(db, new AllowedOpsValidator(db));
        var dto = new AssessmentUpsertDto(
            DateOnly.FromDateTime(DateTime.Today), false, null, null, 300m,
            DateOnly.FromDateTime(DateTime.Today), false, null,
            new[] { new WorkItemUpsertDto(null, panelId, "x",
                polish, null, null, null, null, repair, null, null, null, null, null) });
        return await handler.Handle(new UpsertAssessmentCommand(caseId, dto), default);
    }

    private static async Task SeedCompanyProfileAsync(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db, decimal vatRate = 24m)
    {
        db.CompanyProfiles.Add(new CompanyProfile
        {
            Name = "Paint Bull",
            AddressLine = "x",
            City = "Athens",
            Phone = "x",
            VatNumber = "x",
            DefaultVatRate = vatRate
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task IssueQuote_ComputesTotalsFromAssessment_AndMarksCurrent()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await SeedAssessmentForCaseAsync(db, caseId, panelAllowed, polish: 100, repair: 200);
        await SeedCompanyProfileAsync(db, vatRate: 24m);

        var handler = new IssueQuoteHandler(db, new TestCurrentUser(), TimeProvider.System, new FakeQuotePdfGenerator(), new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        var quoteId = await handler.Handle(new IssueQuoteCommand(caseId), default);

        var saved = await db.Quotes.AsNoTracking().FirstAsync(q => q.Id == quoteId);
        Assert.Equal(300m, saved.LaborSubtotal);
        Assert.Equal(0m, saved.PartsSubtotal);
        Assert.Equal(300m, saved.Subtotal);
        Assert.Equal(24m, saved.VatRate);
        Assert.Equal(72m, saved.VatAmount);
        Assert.Equal(372m, saved.Total);
        Assert.True(saved.IsCurrent);
        Assert.False(string.IsNullOrEmpty(saved.PdfPath));
        Assert.StartsWith($"Q-{DateTime.UtcNow.Year}-", saved.QuoteNumber);
    }

    [Fact]
    public async Task IssueQuote_FlipsPriorCurrentToFalse()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await SeedAssessmentForCaseAsync(db, caseId, panelAllowed);
        await SeedCompanyProfileAsync(db);

        var handler = new IssueQuoteHandler(db, new TestCurrentUser(), TimeProvider.System, new FakeQuotePdfGenerator(), new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        var first = await handler.Handle(new IssueQuoteCommand(caseId), default);
        var second = await handler.Handle(new IssueQuoteCommand(caseId), default);

        Assert.NotEqual(first, second);
        var firstQuote = await db.Quotes.AsNoTracking().FirstAsync(q => q.Id == first);
        var secondQuote = await db.Quotes.AsNoTracking().FirstAsync(q => q.Id == second);
        Assert.False(firstQuote.IsCurrent);
        Assert.True(secondQuote.IsCurrent);
    }

    [Fact]
    public async Task IssueQuote_FailsWithoutAssessment()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await SeedCompanyProfileAsync(db);

        var handler = new IssueQuoteHandler(db, new TestCurrentUser(), TimeProvider.System, new FakeQuotePdfGenerator(), new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new IssueQuoteCommand(caseId), default));
    }

    [Fact]
    public async Task IssueQuote_AppliesDiscounts()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await SeedAssessmentForCaseAsync(db, caseId, panelAllowed, polish: 100, repair: 200);
        await SeedCompanyProfileAsync(db);

        var handler = new IssueQuoteHandler(db, new TestCurrentUser(), TimeProvider.System, new FakeQuotePdfGenerator(), new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        var quoteId = await handler.Handle(
            new IssueQuoteCommand(caseId, LaborDiscountAmount: 50m), default);

        var saved = await db.Quotes.AsNoTracking().FirstAsync(q => q.Id == quoteId);
        Assert.Equal(250m, saved.Subtotal); // 300 - 50 discount
        Assert.Equal(60m, saved.VatAmount); // 250 * 24%
        Assert.Equal(310m, saved.Total);
    }

    [Fact]
    public async Task UpsertApproval_CreatesNewWithCaseInsurer()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        var handler = new UpsertInsuranceApprovalHandler(db);
        var dto = new InsuranceApprovalUpsertDto(
            LiabilityAccepted: true,
            CustomerParticipation: true,
            ParticipationAmount: 100m,
            ApprovedAmount: 500m,
            ApprovalDate: DateOnly.FromDateTime(DateTime.Today),
            ApprovalStatus: ApprovalStatus.PartialApproval,
            Notes: "ok");

        var approvalId = await handler.Handle(new UpsertInsuranceApprovalCommand(caseId, dto), default);

        var saved = await db.InsuranceApprovals.AsNoTracking().FirstAsync(a => a.Id == approvalId);
        Assert.Equal(ApprovalStatus.PartialApproval, saved.ApprovalStatus);
        Assert.Equal(500m, saved.ApprovedAmount);
        Assert.True(saved.LiabilityAccepted);
        Assert.True(saved.CustomerParticipation);
        Assert.Equal(100m, saved.ParticipationAmount);
        // Insurer is auto-filled from case
        var insurerOnCase = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == caseId).Select(c => c.InsuranceCompanyId).FirstAsync();
        Assert.Equal(insurerOnCase, saved.InsuranceCompanyId);
    }

    [Fact]
    public async Task UpsertApproval_ClearsParticipationAmountWhenFlagFalse()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        var handler = new UpsertInsuranceApprovalHandler(db);
        await handler.Handle(new UpsertInsuranceApprovalCommand(caseId, new InsuranceApprovalUpsertDto(
            true, true, 100m, 500m,
            DateOnly.FromDateTime(DateTime.Today), ApprovalStatus.Approved, null)), default);

        // Now flip CustomerParticipation off.
        await handler.Handle(new UpsertInsuranceApprovalCommand(caseId, new InsuranceApprovalUpsertDto(
            true, false, 100m, 500m,
            DateOnly.FromDateTime(DateTime.Today), ApprovalStatus.Approved, null)), default);

        var saved = await db.InsuranceApprovals.AsNoTracking()
            .FirstAsync(a => a.InsuranceCaseId == caseId);
        Assert.False(saved.CustomerParticipation);
        Assert.Null(saved.ParticipationAmount);
    }

    [Fact]
    public async Task UpsertApprovalValidator_RequiresParticipationAmountWhenFlagTrue()
    {
        var validator = new UpsertInsuranceApprovalValidator();
        var dto = new InsuranceApprovalUpsertDto(
            LiabilityAccepted: true,
            CustomerParticipation: true,
            ParticipationAmount: null,
            ApprovedAmount: 500m,
            ApprovalDate: DateOnly.FromDateTime(DateTime.Today),
            ApprovalStatus: ApprovalStatus.Approved,
            Notes: null);

        var result = await validator.ValidateAsync(
            new UpsertInsuranceApprovalCommand(Guid.NewGuid(), dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("ParticipationAmount"));
    }
}
