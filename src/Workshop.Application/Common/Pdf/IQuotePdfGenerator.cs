namespace Workshop.Application.Common.Pdf;

/// <summary>
/// Renders a Quote PDF and stores it via IFileStore. Returns the relative path
/// that should be persisted on Quote.PdfPath.
/// </summary>
public interface IQuotePdfGenerator
{
    Task<string> GenerateAsync(Guid quoteId, CancellationToken ct = default);
}
