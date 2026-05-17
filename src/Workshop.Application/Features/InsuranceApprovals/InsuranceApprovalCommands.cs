using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsuranceApprovals;

public record UpsertInsuranceApprovalCommand(Guid InsuranceCaseId, InsuranceApprovalUpsertDto Data) : IRequest<Guid>;

public class UpsertInsuranceApprovalHandler(IWorkshopDbContext db)
    : IRequestHandler<UpsertInsuranceApprovalCommand, Guid>
{
    public async Task<Guid> Handle(UpsertInsuranceApprovalCommand cmd, CancellationToken ct)
    {
        var caseInfo = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == cmd.InsuranceCaseId)
            .Select(c => new { c.Id, c.InsuranceCompanyId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Insurance case {cmd.InsuranceCaseId} not found");

        var approval = await db.InsuranceApprovals
            .FirstOrDefaultAsync(a => a.InsuranceCaseId == cmd.InsuranceCaseId, ct);

        if (approval is null)
        {
            approval = new InsuranceApproval
            {
                InsuranceCaseId = cmd.InsuranceCaseId,
                InsuranceCompanyId = caseInfo.InsuranceCompanyId
            };
            db.InsuranceApprovals.Add(approval);
        }

        var d = cmd.Data;
        approval.LiabilityAccepted = d.LiabilityAccepted;
        approval.CustomerParticipation = d.CustomerParticipation;
        approval.ParticipationAmount = d.CustomerParticipation ? d.ParticipationAmount : null;
        approval.ApprovedAmount = d.ApprovedAmount;
        approval.ApprovalDate = d.ApprovalDate;
        approval.ApprovalStatus = d.ApprovalStatus;
        approval.Notes = d.Notes;

        await db.SaveChangesAsync(ct);
        return approval.Id;
    }
}

public class InsuranceApprovalUpsertValidator : AbstractValidator<InsuranceApprovalUpsertDto>
{
    public InsuranceApprovalUpsertValidator()
    {
        RuleFor(x => x.ApprovalDate).NotEmpty();
        RuleFor(x => x.ApprovedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ParticipationAmount)
            .NotNull().GreaterThanOrEqualTo(0)
            .When(x => x.CustomerParticipation,
                ApplyConditionTo.CurrentValidator);
    }
}

public class UpsertInsuranceApprovalValidator : AbstractValidator<UpsertInsuranceApprovalCommand>
{
    public UpsertInsuranceApprovalValidator()
    {
        RuleFor(x => x.InsuranceCaseId).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new InsuranceApprovalUpsertValidator());
    }
}
