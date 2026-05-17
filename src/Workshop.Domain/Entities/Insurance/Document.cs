using Workshop.Domain.Common;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Insurance;

public class Document : Entity
{
    public Guid? InsuranceCaseId { get; set; }
    public InsuranceCase? InsuranceCase { get; set; }
    public Guid? RetailCaseId { get; set; }
    public RetailCase? RetailCase { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public DocumentType DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public bool SentToInsurance { get; set; }
    public DateTime? SentToInsuranceAt { get; set; }
}

public class Payment : Entity
{
    public Guid? InsuranceCaseId { get; set; }
    public InsuranceCase? InsuranceCase { get; set; }
    public Guid? RetailCaseId { get; set; }
    public RetailCase? RetailCase { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Payer { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}
