using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Quotes;

public record GetCurrentQuoteForCaseQuery(Guid InsuranceCaseId) : IRequest<QuoteDto?>;

public class GetCurrentQuoteForCaseHandler(IWorkshopDbContext db)
    : IRequestHandler<GetCurrentQuoteForCaseQuery, QuoteDto?>
{
    public async Task<QuoteDto?> Handle(GetCurrentQuoteForCaseQuery q, CancellationToken ct) =>
        await db.Quotes.AsNoTracking()
            .Where(x => x.InsuranceCaseId == q.InsuranceCaseId && x.IsCurrent)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new QuoteDto(
                x.Id, x.QuoteNumber, x.IssueDate,
                x.ResponsibleUserId,
                x.ResponsibleUser != null ? x.ResponsibleUser.FullName : "",
                x.LaborSubtotal, x.PartsSubtotal,
                x.LaborDiscountAmount, x.PartsDiscountAmount,
                x.Subtotal, x.VatRate, x.VatAmount, x.Total,
                x.CustomerParticipation, x.Notes, x.PdfPath, x.IsCurrent))
            .FirstOrDefaultAsync(ct);
}
