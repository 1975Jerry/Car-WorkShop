using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.MyData;
using Workshop.Application.Features.MyData;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;
using Workshop.Infrastructure.Persistence;

namespace Workshop.Application.Tests;

public class MyDataSubmissionTests
{
    [Fact]
    public async Task Submit_persists_mark_returned_by_client()
    {
        await using var db = TestDb.NewContext();
        var (_, quoteId) = await SeedAsync(db);

        var client = new RecordingClient();
        var handler = new SubmitQuoteToMyDataHandler(db, client, TimeProvider.System);

        var mark = await handler.Handle(new SubmitQuoteToMyDataCommand(quoteId), CancellationToken.None);

        Assert.Equal("FAKE-MARK-001", mark);
        var quote = await db.Quotes.AsNoTracking().FirstAsync(q => q.Id == quoteId);
        Assert.Equal("FAKE-MARK-001", quote.MyDataMark);
        Assert.NotNull(quote.MyDataSubmittedAt);
        Assert.Single(client.Submitted);
        Assert.Equal("Q-2026-0001", client.Submitted[0].InvoiceNumber);
        Assert.Equal("EL999999999", client.Submitted[0].IssuerVatNumber);
    }

    [Fact]
    public async Task Resubmit_throws_when_mark_already_recorded()
    {
        await using var db = TestDb.NewContext();
        var (_, quoteId) = await SeedAsync(db);
        var quote = await db.Quotes.FirstAsync(q => q.Id == quoteId);
        quote.MyDataMark = "OLD-MARK";
        await db.SaveChangesAsync();

        var handler = new SubmitQuoteToMyDataHandler(db, new RecordingClient(), TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SubmitQuoteToMyDataCommand(quoteId), CancellationToken.None));
    }

    [Fact]
    public async Task Submit_throws_when_client_reports_failure()
    {
        await using var db = TestDb.NewContext();
        var (_, quoteId) = await SeedAsync(db);
        var handler = new SubmitQuoteToMyDataHandler(db, new FailingClient(), TimeProvider.System);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SubmitQuoteToMyDataCommand(quoteId), CancellationToken.None));

        Assert.Contains("AUTH_FAILED", ex.Message);
        // Failure path must not leave a partial MARK behind.
        var quote = await db.Quotes.AsNoTracking().FirstAsync(q => q.Id == quoteId);
        Assert.Null(quote.MyDataMark);
    }

    private static async Task<(Guid CaseId, Guid QuoteId)> SeedAsync(WorkshopDbContext db)
    {
        db.CompanyProfiles.Add(new CompanyProfile
        {
            Name = "Paint Bull", AddressLine = "Test 1", City = "Athens",
            Phone = "210", VatNumber = "EL999999999", DefaultVatRate = 24m,
        });
        var customer = new Customer
        {
            CustomerType = CustomerType.Company,
            CompanyName = "Acme Holdings",
            MobilePhone = "6900000000",
            GdprConsent = true,
            GdprConsentAt = DateTime.UtcNow,
            IsActive = true,
            VatNumber = "EL111111111",
        };
        var branch = new Branch { Name = "Default", Code = "DFLT", AddressLine = "A", City = "Athens", IsActive = true };
        var insuranceCo = new InsuranceCompany { Name = "X", IsActive = true };
        var vehicle = new Vehicle
        {
            Customer = customer, PlateNumber = "ABC-1234",
            Brand = "Toyota", Model = "Yaris", IsActive = true,
        };
        db.Customers.Add(customer);
        db.Branches.Add(branch);
        db.InsuranceCompanies.Add(insuranceCo);
        db.Vehicles.Add(vehicle);

        var insuranceCase = new InsuranceCase
        {
            CaseNumber = "INS-2026-0001",
            Customer = customer,
            Vehicle = vehicle,
            Branch = branch,
            InsuranceCompany = insuranceCo,
            Status = InsuranceCaseStatus.Assessment,
        };
        db.InsuranceCases.Add(insuranceCase);

        var quote = new Quote
        {
            InsuranceCase = insuranceCase,
            QuoteNumber = "Q-2026-0001",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ResponsibleUserId = Guid.NewGuid(),
            LaborSubtotal = 100m, PartsSubtotal = 50m,
            Subtotal = 150m, VatRate = 24m, VatAmount = 36m, Total = 186m,
            IsCurrent = true,
        };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        return (insuranceCase.Id, quote.Id);
    }

    private class RecordingClient : IMyDataClient
    {
        public List<MyDataInvoice> Submitted { get; } = new();

        public Task<MyDataSubmissionResult> SubmitInvoiceAsync(MyDataInvoice invoice, CancellationToken ct = default)
        {
            Submitted.Add(invoice);
            return Task.FromResult(new MyDataSubmissionResult(true, "FAKE-MARK-001", "uid-1", null, null, DateTime.UtcNow));
        }
        public Task<MyDataSubmissionResult> SubmitReceiptAsync(MyDataReceipt receipt, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<MyDataSubmissionResult> CancelAsync(string mark, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private class FailingClient : IMyDataClient
    {
        public Task<MyDataSubmissionResult> SubmitInvoiceAsync(MyDataInvoice invoice, CancellationToken ct = default)
            => Task.FromResult(new MyDataSubmissionResult(false, null, null, "AUTH_FAILED", "invalid credentials", DateTime.UtcNow));
        public Task<MyDataSubmissionResult> SubmitReceiptAsync(MyDataReceipt receipt, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<MyDataSubmissionResult> CancelAsync(string mark, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
