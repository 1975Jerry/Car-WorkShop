namespace Workshop.Application.Features.Quotes;

public record QuoteDto(
    Guid Id,
    string QuoteNumber,
    DateOnly IssueDate,
    Guid ResponsibleUserId,
    string ResponsibleUserName,
    decimal LaborSubtotal,
    decimal PartsSubtotal,
    decimal? LaborDiscountAmount,
    decimal? PartsDiscountAmount,
    decimal Subtotal,
    decimal VatRate,
    decimal VatAmount,
    decimal Total,
    decimal? CustomerParticipation,
    string? Notes,
    string? PdfPath,
    bool IsCurrent);
