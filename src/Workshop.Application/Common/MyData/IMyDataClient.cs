namespace Workshop.Application.Common.MyData;

/// <summary>
/// AADE myDATA client. Real adapter targets the Greek tax authority REST endpoints
/// (sandbox + production). Stub adapter logs the call and returns a fake MARK so
/// the rest of the system can be exercised without live credentials.
///
/// MARK = "Μοναδικός Αριθμός Καταχώρησης" — the unique registration number AADE
/// assigns to every successfully submitted document.
/// </summary>
public interface IMyDataClient
{
    /// <summary>Submit an invoice (Greek: Τιμολόγιο, document type AADE 1.1).</summary>
    Task<MyDataSubmissionResult> SubmitInvoiceAsync(MyDataInvoice invoice, CancellationToken ct = default);

    /// <summary>Submit a receipt (Greek: Απόδειξη Είσπραξης, document type AADE 8.4).</summary>
    Task<MyDataSubmissionResult> SubmitReceiptAsync(MyDataReceipt receipt, CancellationToken ct = default);

    /// <summary>
    /// Mark an already-submitted document as cancelled. AADE returns a new cancellation MARK
    /// which we record alongside the original.
    /// </summary>
    Task<MyDataSubmissionResult> CancelAsync(string mark, CancellationToken ct = default);
}

/// <summary>
/// Document-agnostic submission outcome. Success = MARK populated; failure = ErrorCode + ErrorMessage.
/// Implementations must not throw on remote errors — surface them through this record.
/// </summary>
public record MyDataSubmissionResult(
    bool Success,
    string? Mark,
    string? Uid,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime SubmittedAt);

/// <summary>
/// Minimal invoice payload — what the workshop needs to submit a quote/invoice.
/// The real adapter expands this into the full AADE XML schema.
/// </summary>
public record MyDataInvoice(
    string InvoiceNumber,
    DateOnly IssueDate,
    string IssuerVatNumber,
    string? CounterpartVatNumber,
    string CounterpartName,
    string Currency,
    decimal NetValue,
    decimal VatAmount,
    decimal TotalValue,
    IReadOnlyList<MyDataInvoiceLine> Lines);

public record MyDataInvoiceLine(
    int LineNumber,
    string Description,
    decimal NetValue,
    decimal VatRate,
    decimal VatAmount,
    decimal Quantity);

public record MyDataReceipt(
    string ReceiptNumber,
    DateOnly IssueDate,
    string IssuerVatNumber,
    string PayerName,
    decimal Amount,
    string PaymentMethod);
