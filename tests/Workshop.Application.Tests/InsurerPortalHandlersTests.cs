using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.InsurerPortal;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class InsurerPortalHandlersTests
{
    private static async Task<(Guid insurerA, Guid insurerB, Guid caseA, Guid caseB)>
        SeedTwoInsurersAsync(Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var branch = new Branch { Name = "Athens", Code = "ATH", AddressLine = "x", City = "Athens", IsActive = true };
        var insurerA = new InsuranceCompany { Name = "Ergo", IsActive = true };
        var insurerB = new InsuranceCompany { Name = "Allianz", IsActive = true };
        var custA = new Customer
        {
            CustomerType = CustomerType.Individual, FirstName = "Alice", LastName = "Alpha",
            MobilePhone = "6900000001", GdprConsent = true, GdprConsentAt = DateTime.UtcNow, IsActive = true
        };
        var custB = new Customer
        {
            CustomerType = CustomerType.Individual, FirstName = "Bob", LastName = "Beta",
            MobilePhone = "6900000002", GdprConsent = true, GdprConsentAt = DateTime.UtcNow, IsActive = true
        };
        var vehA = new Vehicle { Customer = custA, PlateNumber = "AAA-1111",
            Brand = "VW", Model = "Polo", IsActive = true };
        var vehB = new Vehicle { Customer = custB, PlateNumber = "BBB-2222",
            Brand = "BMW", Model = "320", IsActive = true };

        var caseA = new InsuranceCase
        {
            CaseNumber = "INS-A-0001", Customer = custA, Vehicle = vehA,
            Branch = branch, InsuranceCompany = insurerA, Status = InsuranceCaseStatus.InsuranceApproval
        };
        var caseB = new InsuranceCase
        {
            CaseNumber = "INS-B-0001", Customer = custB, Vehicle = vehB,
            Branch = branch, InsuranceCompany = insurerB, Status = InsuranceCaseStatus.InsuranceApproval
        };

        db.Branches.Add(branch);
        db.InsuranceCompanies.AddRange(insurerA, insurerB);
        db.Customers.AddRange(custA, custB);
        db.Vehicles.AddRange(vehA, vehB);
        db.InsuranceCases.AddRange(caseA, caseB);
        await db.SaveChangesAsync();

        return (insurerA.Id, insurerB.Id, caseA.Id, caseB.Id);
    }

    [Fact]
    public async Task List_ScopesToOwnCompany()
    {
        await using var db = TestDb.NewContext();
        var (insA, insB, caseA, caseB) = await SeedTwoInsurersAsync(db);

        var aList = await new ListInsurerCasesHandler(db).Handle(
            new ListInsurerCasesQuery(insA), default);
        var bList = await new ListInsurerCasesHandler(db).Handle(
            new ListInsurerCasesQuery(insB), default);

        var rowA = Assert.Single(aList.Items);
        Assert.Equal(caseA, rowA.Id);
        var rowB = Assert.Single(bList.Items);
        Assert.Equal(caseB, rowB.Id);
    }

    [Fact]
    public async Task GetDetail_RefusesOtherCompany()
    {
        await using var db = TestDb.NewContext();
        var (insA, insB, caseA, _) = await SeedTwoInsurersAsync(db);

        Assert.NotNull(await new GetInsurerCaseDetailHandler(db).Handle(
            new GetInsurerCaseDetailQuery(insA, caseA), default));
        Assert.Null(await new GetInsurerCaseDetailHandler(db).Handle(
            new GetInsurerCaseDetailQuery(insB, caseA), default));
    }

    [Fact]
    public async Task Decide_Approve_PersistsAmountAndStatus()
    {
        await using var db = TestDb.NewContext();
        var (insA, _, caseA, _) = await SeedTwoInsurersAsync(db);

        await new InsurerDecideHandler(db, TimeProvider.System).Handle(
            new InsurerDecideCommand(insA, caseA, ApprovalStatus.Approved,
                new InsurerDecisionDto(1500m, "Approved per assessment")), default);

        var approval = await db.InsuranceApprovals.AsNoTracking()
            .FirstAsync(a => a.InsuranceCaseId == caseA);
        Assert.Equal(ApprovalStatus.Approved, approval.ApprovalStatus);
        Assert.Equal(1500m, approval.ApprovedAmount);
        Assert.Equal("Approved per assessment", approval.Notes);
    }

    [Fact]
    public async Task Decide_Reject_ZerosApprovedAmount()
    {
        await using var db = TestDb.NewContext();
        var (insA, _, caseA, _) = await SeedTwoInsurersAsync(db);

        // First approve, then reject — verify ApprovedAmount gets zeroed.
        await new InsurerDecideHandler(db, TimeProvider.System).Handle(
            new InsurerDecideCommand(insA, caseA, ApprovalStatus.Approved,
                new InsurerDecisionDto(1500m, null)), default);
        await new InsurerDecideHandler(db, TimeProvider.System).Handle(
            new InsurerDecideCommand(insA, caseA, ApprovalStatus.Rejected,
                new InsurerDecisionDto(0m, "Out of scope")), default);

        var approval = await db.InsuranceApprovals.AsNoTracking()
            .FirstAsync(a => a.InsuranceCaseId == caseA);
        Assert.Equal(ApprovalStatus.Rejected, approval.ApprovalStatus);
        Assert.Equal(0m, approval.ApprovedAmount);
        Assert.Equal("Out of scope", approval.Notes);
    }

    [Fact]
    public async Task Decide_RefusesOtherCompany()
    {
        await using var db = TestDb.NewContext();
        var (_, insB, caseA, _) = await SeedTwoInsurersAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new InsurerDecideHandler(db, TimeProvider.System).Handle(
                new InsurerDecideCommand(insB, caseA, ApprovalStatus.Approved,
                    new InsurerDecisionDto(500m, null)), default));
    }

    [Fact]
    public async Task Decide_PendingNotAllowed()
    {
        await using var db = TestDb.NewContext();
        var (insA, _, caseA, _) = await SeedTwoInsurersAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new InsurerDecideHandler(db, TimeProvider.System).Handle(
                new InsurerDecideCommand(insA, caseA, ApprovalStatus.Pending,
                    new InsurerDecisionDto(0m, null)), default));
    }

    [Fact]
    public async Task DecideValidator_RejectRequiresNotes()
    {
        var v = new InsurerDecideValidator();
        var cmd = new InsurerDecideCommand(Guid.NewGuid(), Guid.NewGuid(),
            ApprovalStatus.Rejected, new InsurerDecisionDto(0m, null));
        var result = await v.ValidateAsync(cmd);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("Notes"));
    }

    [Fact]
    public async Task SentDocuments_FiltersUnsentAndOtherCompany()
    {
        await using var db = TestDb.NewContext();
        var (insA, insB, caseA, _) = await SeedTwoInsurersAsync(db);
        db.Documents.AddRange(
            new Document
            {
                InsuranceCaseId = caseA, DocumentType = DocumentType.CaseForm,
                FileName = "x.pdf", FilePath = "uploads/x.pdf", ContentType = "application/pdf",
                SizeBytes = 1, UploadedById = Guid.NewGuid(),
                SentToInsurance = true, SentToInsuranceAt = DateTime.UtcNow
            },
            new Document
            {
                InsuranceCaseId = caseA, DocumentType = DocumentType.Invoice,
                FileName = "y.pdf", FilePath = "uploads/y.pdf", ContentType = "application/pdf",
                SizeBytes = 1, UploadedById = Guid.NewGuid(),
                SentToInsurance = false
            });
        await db.SaveChangesAsync();

        var sent = await new GetInsurerSentDocumentsHandler(db).Handle(
            new GetInsurerSentDocumentsQuery(insA, caseA), default);
        var doc = Assert.Single(sent);
        Assert.Equal(DocumentType.CaseForm, doc.DocumentType);

        // Other insurer sees nothing.
        var other = await new GetInsurerSentDocumentsHandler(db).Handle(
            new GetInsurerSentDocumentsQuery(insB, caseA), default);
        Assert.Empty(other);
    }
}
