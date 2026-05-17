using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.CustomerPortal;
using Workshop.Application.Features.RetailCases;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class CustomerPortalHandlersTests
{
    private static async Task<(Guid customerA, Guid customerB, Guid insuranceCaseA, Guid retailCaseA)>
        SeedTwoCustomersAsync(Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var branch = new Branch { Name = "Athens", Code = "ATH", AddressLine = "x", City = "Athens", IsActive = true };
        var insurer = new InsuranceCompany { Name = "Ergo", IsActive = true };
        var customerA = new Customer
        {
            CustomerType = CustomerType.Individual, FirstName = "Alice", LastName = "Alpha",
            MobilePhone = "6900000001", GdprConsent = true, GdprConsentAt = DateTime.UtcNow, IsActive = true
        };
        var customerB = new Customer
        {
            CustomerType = CustomerType.Individual, FirstName = "Bob", LastName = "Beta",
            MobilePhone = "6900000002", GdprConsent = true, GdprConsentAt = DateTime.UtcNow, IsActive = true
        };
        var vehicleA = new Vehicle { Customer = customerA, PlateNumber = "AAA-1111",
            Brand = "VW", Model = "Polo", IsActive = true };
        var vehicleB = new Vehicle { Customer = customerB, PlateNumber = "BBB-2222",
            Brand = "BMW", Model = "320", IsActive = true };

        var insuranceA = new InsuranceCase
        {
            CaseNumber = "INS-A-0001", Customer = customerA, Vehicle = vehicleA,
            Branch = branch, InsuranceCompany = insurer, Status = InsuranceCaseStatus.NewCase
        };
        var insuranceB = new InsuranceCase
        {
            CaseNumber = "INS-B-0001", Customer = customerB, Vehicle = vehicleB,
            Branch = branch, InsuranceCompany = insurer, Status = InsuranceCaseStatus.NewCase
        };

        db.Branches.Add(branch);
        db.InsuranceCompanies.Add(insurer);
        db.Customers.AddRange(customerA, customerB);
        db.Vehicles.AddRange(vehicleA, vehicleB);
        db.InsuranceCases.AddRange(insuranceA, insuranceB);
        await db.SaveChangesAsync();

        var retailA = await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                customerA.Id, vehicleA.Id, branch.Id, null, "Polish", 100m, 24m, null, null)), default);

        return (customerA.Id, customerB.Id, insuranceA.Id, retailA);
    }

    [Fact]
    public async Task ListMyCases_ReturnsOnlyOwnCases()
    {
        await using var db = TestDb.NewContext();
        var (custA, custB, _, _) = await SeedTwoCustomersAsync(db);

        var aRows = await new ListMyCasesHandler(db).Handle(new ListMyCasesQuery(custA), default);
        var bRows = await new ListMyCasesHandler(db).Handle(new ListMyCasesQuery(custB), default);

        Assert.Equal(2, aRows.Count); // insurance + retail
        Assert.All(aRows, r => Assert.True(
            r.CaseNumber.StartsWith("INS-A-") || r.CaseNumber.StartsWith("RET-")));
        Assert.Single(bRows);
        Assert.StartsWith("INS-B-", bRows[0].CaseNumber);
    }

    [Fact]
    public async Task GetMyInsuranceCase_RefusesOtherCustomer()
    {
        await using var db = TestDb.NewContext();
        var (custA, custB, insA, _) = await SeedTwoCustomersAsync(db);

        var asOwner = await new GetMyInsuranceCaseHandler(db).Handle(
            new GetMyInsuranceCaseQuery(custA, insA), default);
        Assert.NotNull(asOwner);

        var asOther = await new GetMyInsuranceCaseHandler(db).Handle(
            new GetMyInsuranceCaseQuery(custB, insA), default);
        Assert.Null(asOther);
    }

    [Fact]
    public async Task GetMyRetailCase_RefusesOtherCustomer()
    {
        await using var db = TestDb.NewContext();
        var (custA, custB, _, retA) = await SeedTwoCustomersAsync(db);

        Assert.NotNull(await new GetMyRetailCaseHandler(db).Handle(
            new GetMyRetailCaseQuery(custA, retA), default));
        Assert.Null(await new GetMyRetailCaseHandler(db).Handle(
            new GetMyRetailCaseQuery(custB, retA), default));
    }

    [Fact]
    public async Task GetMyCaseDocuments_FiltersInternalDocTypes()
    {
        await using var db = TestDb.NewContext();
        var (custA, custB, insA, _) = await SeedTwoCustomersAsync(db);

        // Upload a mix of doc types.
        db.Documents.AddRange(
            new Workshop.Domain.Entities.Insurance.Document
            {
                InsuranceCaseId = insA, DocumentType = DocumentType.CaseForm,
                FileName = "case.pdf", FilePath = "uploads/case.pdf",
                ContentType = "application/pdf", SizeBytes = 1, UploadedById = Guid.NewGuid()
            },
            new Workshop.Domain.Entities.Insurance.Document
            {
                InsuranceCaseId = insA, DocumentType = DocumentType.Invoice,
                FileName = "invoice.pdf", FilePath = "uploads/invoice.pdf",
                ContentType = "application/pdf", SizeBytes = 1, UploadedById = Guid.NewGuid()
            },
            new Workshop.Domain.Entities.Insurance.Document
            {
                InsuranceCaseId = insA, DocumentType = DocumentType.InsuranceForm,
                FileName = "insform.pdf", FilePath = "uploads/insform.pdf",
                ContentType = "application/pdf", SizeBytes = 1, UploadedById = Guid.NewGuid()
            });
        await db.SaveChangesAsync();

        var visible = await new GetMyCaseDocumentsHandler(db).Handle(
            new GetMyCaseDocumentsQuery(custA, insA, PortalCaseKind.Insurance), default);
        var row = Assert.Single(visible);
        Assert.Equal(DocumentType.Invoice, row.DocumentType);

        // Other customer sees nothing.
        var other = await new GetMyCaseDocumentsHandler(db).Handle(
            new GetMyCaseDocumentsQuery(custB, insA, PortalCaseKind.Insurance), default);
        Assert.Empty(other);
    }

    [Fact]
    public async Task GetMyCaseEvents_RefusesOtherCustomer()
    {
        await using var db = TestDb.NewContext();
        var (custA, custB, insA, _) = await SeedTwoCustomersAsync(db);

        // Trigger an event by adding directly (we don't need a full state machine fire here).
        db.CaseEvents.Add(new Workshop.Domain.Entities.CrossCutting.CaseEvent
        {
            InsuranceCaseId = insA, FromStatus = "NewCase", ToStatus = "AssessorAppointment",
            TriggeredById = Guid.NewGuid(), OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var ownerEvents = await new GetMyCaseEventsHandler(db).Handle(
            new GetMyCaseEventsQuery(custA, insA, PortalCaseKind.Insurance), default);
        Assert.Single(ownerEvents);

        var otherEvents = await new GetMyCaseEventsHandler(db).Handle(
            new GetMyCaseEventsQuery(custB, insA, PortalCaseKind.Insurance), default);
        Assert.Empty(otherEvents);
    }
}
