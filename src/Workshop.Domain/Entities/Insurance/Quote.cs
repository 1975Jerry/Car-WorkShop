using Workshop.Domain.Common;
using Workshop.Domain.Entities.Identity;

namespace Workshop.Domain.Entities.Insurance;

public class Quote : Entity
{
    public Guid InsuranceCaseId { get; set; }
    public InsuranceCase InsuranceCase { get; set; } = null!;
    public string QuoteNumber { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public Guid ResponsibleUserId { get; set; }
    public User ResponsibleUser { get; set; } = null!;
    public decimal LaborSubtotal { get; set; }
    public decimal PartsSubtotal { get; set; }
    public decimal? LaborDiscountAmount { get; set; }
    public decimal? PartsDiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public decimal? CustomerParticipation { get; set; }
    public string? Notes { get; set; }
    public string? PdfPath { get; set; }
    public bool IsCurrent { get; set; }

    public string? MyDataMark { get; set; }
    public DateTime? MyDataSubmittedAt { get; set; }
}
