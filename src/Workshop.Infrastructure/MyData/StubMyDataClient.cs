using Microsoft.Extensions.Logging;
using Workshop.Application.Common.MyData;

namespace Workshop.Infrastructure.MyData;

/// <summary>
/// Phase 11 placeholder. Returns a deterministic-looking fake MARK so the rest
/// of the pipeline can be exercised without an AADE sandbox account.
/// Swap with a real HTTP client adapter when credentials are configured.
/// </summary>
public class StubMyDataClient(ILogger<StubMyDataClient> log, TimeProvider clock) : IMyDataClient
{
    public Task<MyDataSubmissionResult> SubmitInvoiceAsync(MyDataInvoice invoice, CancellationToken ct = default)
    {
        log.LogInformation(
            "[myDATA STUB] invoice {Number} issuer={IssuerVat} counterpart={CounterVat} total={Total:N2} ({Currency})",
            invoice.InvoiceNumber, invoice.IssuerVatNumber, invoice.CounterpartVatNumber ?? "—",
            invoice.TotalValue, invoice.Currency);
        return Task.FromResult(Success("INV"));
    }

    public Task<MyDataSubmissionResult> SubmitReceiptAsync(MyDataReceipt receipt, CancellationToken ct = default)
    {
        log.LogInformation(
            "[myDATA STUB] receipt {Number} issuer={IssuerVat} payer={Payer} amount={Amount:N2}",
            receipt.ReceiptNumber, receipt.IssuerVatNumber, receipt.PayerName, receipt.Amount);
        return Task.FromResult(Success("RCP"));
    }

    public Task<MyDataSubmissionResult> CancelAsync(string mark, CancellationToken ct = default)
    {
        log.LogInformation("[myDATA STUB] cancel MARK={Mark}", mark);
        return Task.FromResult(Success("CXL"));
    }

    private MyDataSubmissionResult Success(string prefix)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // AADE MARKs are 15-digit numbers. The stub generates a value with the same shape so
        // downstream code that pattern-matches on length doesn't need a special case for tests.
        var mark = $"{now:yyyyMMddHHmmss}{Random.Shared.Next(0, 10)}";
        var uid = $"STUB-{prefix}-{Guid.NewGuid():N}";
        return new MyDataSubmissionResult(true, mark, uid, null, null, now);
    }
}
