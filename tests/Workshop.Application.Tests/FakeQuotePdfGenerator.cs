using Workshop.Application.Common.Pdf;

namespace Workshop.Application.Tests;

internal class FakeQuotePdfGenerator : IQuotePdfGenerator
{
    public List<Guid> GeneratedFor { get; } = new();

    public Task<string> GenerateAsync(Guid quoteId, CancellationToken ct = default)
    {
        GeneratedFor.Add(quoteId);
        return Task.FromResult($"uploads/quotes/{quoteId:N}.pdf");
    }
}
