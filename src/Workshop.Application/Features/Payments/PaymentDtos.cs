using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Payments;

public record PaymentDto(
    Guid Id,
    Guid InsuranceCaseId,
    decimal Amount,
    DateOnly PaymentDate,
    PaymentMethod PaymentMethod,
    string? Payer,
    string? ReferenceNumber,
    string? Notes,
    DateTime CreatedAt);

public record CreatePaymentDto(
    decimal Amount,
    DateOnly PaymentDate,
    PaymentMethod PaymentMethod,
    string? Payer,
    string? ReferenceNumber,
    string? Notes);

public record CaseSettlementSummaryDto(
    decimal AgreedAmount,
    decimal TotalPaid,
    decimal RemainingBalance,
    bool IsFullyPaid,
    int RequiredDocumentsSentCount);
