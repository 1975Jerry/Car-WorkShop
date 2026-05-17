using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsuranceApprovals;

public record InsuranceApprovalReadDto(
    Guid? Id,
    Guid InsuranceCaseId,
    Guid InsuranceCompanyId,
    string InsuranceCompanyName,
    bool LiabilityAccepted,
    bool CustomerParticipation,
    decimal? ParticipationAmount,
    decimal ApprovedAmount,
    DateOnly ApprovalDate,
    ApprovalStatus ApprovalStatus,
    string? Notes,
    DateTime? UpdatedAt);

public record InsuranceApprovalUpsertDto(
    bool LiabilityAccepted,
    bool CustomerParticipation,
    decimal? ParticipationAmount,
    decimal ApprovedAmount,
    DateOnly ApprovalDate,
    ApprovalStatus ApprovalStatus,
    string? Notes);
