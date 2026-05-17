using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.InsuranceApprovals;
using Workshop.Application.Features.Payments;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class PaymentHandlersTests
{
    private static async Task<Guid> SeedCaseWithApprovedAmountAsync(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db, decimal approvedAmount)
    {
        var (caseId, _, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        await new UpsertInsuranceApprovalHandler(db).Handle(
            new UpsertInsuranceApprovalCommand(caseId, new InsuranceApprovalUpsertDto(
                LiabilityAccepted: true,
                CustomerParticipation: false,
                ParticipationAmount: null,
                ApprovedAmount: approvedAmount,
                ApprovalDate: DateOnly.FromDateTime(DateTime.Today),
                ApprovalStatus: ApprovalStatus.Approved,
                Notes: null)), default);
        return caseId;
    }

    [Fact]
    public async Task CreatePayment_PersistsAndSumsInSummary()
    {
        await using var db = TestDb.NewContext();
        var caseId = await SeedCaseWithApprovedAmountAsync(db, approvedAmount: 1000m);

        var create = new CreatePaymentHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        await create.Handle(new CreatePaymentCommand(caseId, new CreatePaymentDto(
            400m, DateOnly.FromDateTime(DateTime.Today), PaymentMethod.BankTransfer,
            "Acme insurance", "REF-1", null)), default);
        await create.Handle(new CreatePaymentCommand(caseId, new CreatePaymentDto(
            300m, DateOnly.FromDateTime(DateTime.Today), PaymentMethod.Cash,
            "Customer", null, null)), default);

        var summary = await new GetCaseSettlementSummaryHandler(db).Handle(
            new GetCaseSettlementSummaryQuery(caseId), default);
        Assert.Equal(1000m, summary.AgreedAmount);
        Assert.Equal(700m, summary.TotalPaid);
        Assert.Equal(300m, summary.RemainingBalance);
        Assert.False(summary.IsFullyPaid);
    }

    [Fact]
    public async Task CreatePayment_FullPayment_FlagsFullyPaid()
    {
        await using var db = TestDb.NewContext();
        var caseId = await SeedCaseWithApprovedAmountAsync(db, approvedAmount: 500m);
        await new CreatePaymentHandler(db, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients()).Handle(new CreatePaymentCommand(caseId,
            new CreatePaymentDto(500m, DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod.InsurancePayout, "Acme", null, null)), default);

        var summary = await new GetCaseSettlementSummaryHandler(db).Handle(
            new GetCaseSettlementSummaryQuery(caseId), default);
        Assert.True(summary.IsFullyPaid);
        Assert.Equal(0m, summary.RemainingBalance);
    }

    [Fact]
    public async Task DeletePayment_RemovesFromSummary()
    {
        await using var db = TestDb.NewContext();
        var caseId = await SeedCaseWithApprovedAmountAsync(db, approvedAmount: 1000m);
        var payment = new Workshop.Domain.Entities.Insurance.Payment
        {
            InsuranceCaseId = caseId,
            Amount = 600m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = PaymentMethod.Cash
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        await new DeletePaymentHandler(db).Handle(new DeletePaymentCommand(payment.Id), default);

        var summary = await new GetCaseSettlementSummaryHandler(db).Handle(
            new GetCaseSettlementSummaryQuery(caseId), default);
        Assert.Equal(0m, summary.TotalPaid);
    }

    [Fact]
    public async Task CreatePaymentValidator_RejectsZeroOrNegative()
    {
        var validator = new CreatePaymentValidator();
        var zero = new CreatePaymentCommand(Guid.NewGuid(), new CreatePaymentDto(
            0m, DateOnly.FromDateTime(DateTime.Today), PaymentMethod.Cash, null, null, null));
        var neg = zero with { Data = zero.Data with { Amount = -10m } };

        Assert.False((await validator.ValidateAsync(zero)).IsValid);
        Assert.False((await validator.ValidateAsync(neg)).IsValid);
    }

    [Fact]
    public async Task SettlementSummary_CountsOnlySentDocuments()
    {
        await using var db = TestDb.NewContext();
        var caseId = await SeedCaseWithApprovedAmountAsync(db, approvedAmount: 100m);

        db.Documents.Add(new Workshop.Domain.Entities.Insurance.Document
        {
            InsuranceCaseId = caseId,
            DocumentType = DocumentType.CaseForm,
            FileName = "x.pdf", FilePath = "uploads/x.pdf", ContentType = "application/pdf",
            SizeBytes = 1, UploadedById = Guid.NewGuid(), SentToInsurance = true
        });
        db.Documents.Add(new Workshop.Domain.Entities.Insurance.Document
        {
            InsuranceCaseId = caseId,
            DocumentType = DocumentType.Invoice,
            FileName = "y.pdf", FilePath = "uploads/y.pdf", ContentType = "application/pdf",
            SizeBytes = 1, UploadedById = Guid.NewGuid(), SentToInsurance = false
        });
        await db.SaveChangesAsync();

        var summary = await new GetCaseSettlementSummaryHandler(db).Handle(
            new GetCaseSettlementSummaryQuery(caseId), default);
        Assert.Equal(1, summary.RequiredDocumentsSentCount);
    }
}
