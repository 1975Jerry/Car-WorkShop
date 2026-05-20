using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Notifications;
using Workshop.Application.Common.Pdf;
using Workshop.Domain.Entities.Insurance;

namespace Workshop.Application.Features.Quotes;

public record IssueQuoteCommand(
    Guid InsuranceCaseId,
    decimal? LaborDiscountAmount = null,
    decimal? PartsDiscountAmount = null,
    string? Notes = null) : IRequest<Guid>;

public class IssueQuoteHandler(
    IWorkshopDbContext db,
    ICurrentUserService user,
    TimeProvider clock,
    IQuotePdfGenerator pdf,
    INotificationDispatcher notifications,
    ICaseNotificationRecipients recipients)
    : IRequestHandler<IssueQuoteCommand, Guid>
{
    public async Task<Guid> Handle(IssueQuoteCommand cmd, CancellationToken ct)
    {
        if (user.UserId is null)
            throw new InvalidOperationException("Must be authenticated to issue a quote.");

        var data = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == cmd.InsuranceCaseId)
            .Select(c => new
            {
                Assessment = c.Assessment,
                LaborSubtotal = c.Assessment != null
                    ? c.Assessment.WorkItems.Sum(w => (decimal?)w.Total) ?? 0m
                    : 0m,
                PartsSubtotal = c.Assessment != null
                    ? c.Assessment.PartLines.Sum(p => (decimal?)p.Total) ?? 0m
                    : 0m,
                CustomerParticipationAmount = c.Approval != null && c.Approval.CustomerParticipation
                    ? c.Approval.ParticipationAmount
                    : null
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Insurance case {cmd.InsuranceCaseId} not found");

        if (data.Assessment is null)
            throw new InvalidOperationException("Cannot issue a quote before the Assessment is recorded.");

        var vatRate = await db.CompanyProfiles.AsNoTracking()
            .Select(p => (decimal?)p.DefaultVatRate)
            .FirstOrDefaultAsync(ct) ?? 24.00m;

        // Mark any prior current quote as non-current.
        var priorCurrent = await db.Quotes
            .Where(q => q.InsuranceCaseId == cmd.InsuranceCaseId && q.IsCurrent)
            .ToListAsync(ct);
        foreach (var p in priorCurrent) p.IsCurrent = false;

        var year = clock.GetUtcNow().Year;
        var prefix = $"Q-{year}-";
        var yearCount = await db.Quotes.CountAsync(q => q.QuoteNumber.StartsWith(prefix), ct);
        var quoteNumber = $"{prefix}{(yearCount + 1).ToString("D4")}";
        while (await db.Quotes.AnyAsync(q => q.QuoteNumber == quoteNumber, ct))
        {
            yearCount++;
            quoteNumber = $"{prefix}{(yearCount + 1).ToString("D4")}";
        }

        var labor = Math.Max(0, data.LaborSubtotal - (cmd.LaborDiscountAmount ?? 0));
        var parts = Math.Max(0, data.PartsSubtotal - (cmd.PartsDiscountAmount ?? 0));
        var subtotal = Math.Round(labor + parts, 2, MidpointRounding.AwayFromZero);
        var vatAmount = Math.Round(subtotal * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + vatAmount;

        var quote = new Quote
        {
            InsuranceCaseId = cmd.InsuranceCaseId,
            QuoteNumber = quoteNumber,
            IssueDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
            ResponsibleUserId = user.UserId.Value,
            LaborSubtotal = data.LaborSubtotal,
            PartsSubtotal = data.PartsSubtotal,
            LaborDiscountAmount = cmd.LaborDiscountAmount,
            PartsDiscountAmount = cmd.PartsDiscountAmount,
            Subtotal = subtotal,
            VatRate = vatRate,
            VatAmount = vatAmount,
            Total = total,
            CustomerParticipation = data.CustomerParticipationAmount,
            Notes = cmd.Notes,
            IsCurrent = true
        };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync(ct);

        // Render PDF after the Quote row exists so the generator can read it back.
        quote.PdfPath = await pdf.GenerateAsync(quote.Id, ct);
        await db.SaveChangesAsync(ct);

        var to = await recipients.ResolveAsync(
            cmd.InsuranceCaseId, null,
            CaseAudienceFlags.Customer | CaseAudienceFlags.AssignedStaff,
            ct);
        if (to.Count > 0)
        {
            await notifications.DispatchAsync(new NotificationRequest(
                Kind: NotificationKind.QuoteIssued,
                TitleGr: $"Νέα Προσφορά {quote.QuoteNumber}",
                TitleEn: $"New quote {quote.QuoteNumber}",
                BodyGr: $"Εκδόθηκε προσφορά συνολικού ποσού {quote.Total:N2} €.",
                BodyEn: $"Quote issued, total {quote.Total:N2} €.",
                Url: $"/cases/insurance/{cmd.InsuranceCaseId}",
                Recipients: to), ct);
        }

        return quote.Id;
    }
}

public record RegenerateQuotePdfCommand(Guid QuoteId) : IRequest<string>;

public class RegenerateQuotePdfHandler(IWorkshopDbContext db, IQuotePdfGenerator pdf)
    : IRequestHandler<RegenerateQuotePdfCommand, string>
{
    public async Task<string> Handle(RegenerateQuotePdfCommand cmd, CancellationToken ct)
    {
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == cmd.QuoteId, ct)
            ?? throw new KeyNotFoundException($"Quote {cmd.QuoteId} not found");
        quote.PdfPath = await pdf.GenerateAsync(quote.Id, ct);
        await db.SaveChangesAsync(ct);
        return quote.PdfPath;
    }
}

public class IssueQuoteValidator : AbstractValidator<IssueQuoteCommand>
{
    public IssueQuoteValidator()
    {
        RuleFor(x => x.InsuranceCaseId).NotEmpty();
        RuleFor(x => x.LaborDiscountAmount).GreaterThanOrEqualTo(0).When(x => x.LaborDiscountAmount.HasValue);
        RuleFor(x => x.PartsDiscountAmount).GreaterThanOrEqualTo(0).When(x => x.PartsDiscountAmount.HasValue);
    }
}
