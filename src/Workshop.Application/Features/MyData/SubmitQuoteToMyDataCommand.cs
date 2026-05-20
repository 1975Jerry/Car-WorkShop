using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.MyData;

namespace Workshop.Application.Features.MyData;

/// <summary>
/// Submit a quote to AADE myDATA as an invoice. On success persists the returned MARK
/// onto the Quote row. Failures bubble up as InvalidOperationException with the AADE
/// error code so the UI can show a meaningful message.
/// </summary>
public record SubmitQuoteToMyDataCommand(Guid QuoteId) : IRequest<string>;

public class SubmitQuoteToMyDataHandler(IWorkshopDbContext db, IMyDataClient client)
    : IRequestHandler<SubmitQuoteToMyDataCommand, string>
{
    public async Task<string> Handle(SubmitQuoteToMyDataCommand cmd, CancellationToken ct)
    {
        var quote = await db.Quotes
            .FirstOrDefaultAsync(q => q.Id == cmd.QuoteId, ct)
            ?? throw new KeyNotFoundException($"Quote {cmd.QuoteId} not found");

        // Skip if already submitted — caller should issue a cancellation MARK first.
        if (!string.IsNullOrEmpty(quote.MyDataMark))
            throw new InvalidOperationException(
                $"Quote {quote.QuoteNumber} already submitted (MARK {quote.MyDataMark}). Cancel before resubmitting.");

        var caseRow = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == quote.InsuranceCaseId)
            .Select(c => new { c.CustomerId, c.BranchId })
            .FirstAsync(ct);

        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.Id == caseRow.CustomerId)
            .Select(c => new { c.FirstName, c.LastName, c.CompanyName, c.VatNumber })
            .FirstAsync(ct);

        var company = await db.CompanyProfiles.AsNoTracking()
            .Select(p => new { p.VatNumber })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Company profile missing — cannot submit to myDATA.");

        var counterpartName = !string.IsNullOrWhiteSpace(customer.CompanyName)
            ? customer.CompanyName!
            : $"{customer.FirstName} {customer.LastName}".Trim();

        var invoice = new MyDataInvoice(
            InvoiceNumber: quote.QuoteNumber,
            IssueDate: quote.IssueDate,
            IssuerVatNumber: company.VatNumber,
            CounterpartVatNumber: string.IsNullOrWhiteSpace(customer.VatNumber) ? null : customer.VatNumber,
            CounterpartName: string.IsNullOrWhiteSpace(counterpartName) ? "—" : counterpartName,
            Currency: "EUR",
            NetValue: quote.Subtotal,
            VatAmount: quote.VatAmount,
            TotalValue: quote.Total,
            Lines:
            [
                new MyDataInvoiceLine(1, "Εργασίες (Labor)", quote.LaborSubtotal - (quote.LaborDiscountAmount ?? 0), quote.VatRate, 0m, 1m),
                new MyDataInvoiceLine(2, "Ανταλλακτικά (Parts)", quote.PartsSubtotal - (quote.PartsDiscountAmount ?? 0), quote.VatRate, 0m, 1m),
            ]);

        var result = await client.SubmitInvoiceAsync(invoice, ct);
        if (!result.Success || string.IsNullOrEmpty(result.Mark))
            throw new InvalidOperationException(
                $"myDATA submission failed ({result.ErrorCode ?? "UNKNOWN"}): {result.ErrorMessage ?? "no detail"}");

        quote.MyDataMark = result.Mark;
        quote.MyDataSubmittedAt = result.SubmittedAt;
        await db.SaveChangesAsync(ct);

        return result.Mark;
    }
}
