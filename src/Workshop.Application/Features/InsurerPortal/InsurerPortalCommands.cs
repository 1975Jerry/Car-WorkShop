using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Notifications;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsurerPortal;

/// <summary>
/// Insurer-side approval/rejection. Strictly scoped — the case must belong to
/// the same insurance company that the calling user represents. Only sets
/// ApprovalStatus, ApprovedAmount, ApprovalDate, and Notes; staff-side fields
/// (liability/participation) stay untouched.
/// </summary>
public record InsurerDecideCommand(
    Guid InsuranceCompanyId,
    Guid InsuranceCaseId,
    ApprovalStatus Decision,
    InsurerDecisionDto Data) : IRequest;

public class InsurerDecideHandler(
    IWorkshopDbContext db,
    TimeProvider clock,
    INotificationDispatcher notifications,
    ICaseNotificationRecipients recipients)
    : IRequestHandler<InsurerDecideCommand>
{
    public async Task Handle(InsurerDecideCommand cmd, CancellationToken ct)
    {
        if (cmd.Decision is not (ApprovalStatus.Approved or ApprovalStatus.Rejected))
            throw new InvalidOperationException(
                "Insurer can only set Approved or Rejected. Pending is a staff-side state.");

        var caseRow = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == cmd.InsuranceCaseId)
            .Select(c => new { c.Id, c.CaseNumber, c.InsuranceCompanyId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Insurance case {cmd.InsuranceCaseId} not found");

        if (caseRow.InsuranceCompanyId != cmd.InsuranceCompanyId)
            throw new UnauthorizedAccessException(
                "This case does not belong to the calling insurance company.");

        var approval = await db.InsuranceApprovals
            .FirstOrDefaultAsync(a => a.InsuranceCaseId == cmd.InsuranceCaseId, ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        if (approval is null)
        {
            approval = new InsuranceApproval
            {
                InsuranceCaseId = cmd.InsuranceCaseId,
                InsuranceCompanyId = caseRow.InsuranceCompanyId,
                LiabilityAccepted = false,
                CustomerParticipation = false,
                ApprovalDate = today
            };
            db.InsuranceApprovals.Add(approval);
        }

        approval.ApprovalStatus = cmd.Decision;
        approval.ApprovedAmount = cmd.Decision == ApprovalStatus.Approved ? cmd.Data.ApprovedAmount : 0m;
        approval.ApprovalDate = today;
        approval.Notes = cmd.Data.Notes;

        await db.SaveChangesAsync(ct);

        var to = await recipients.ResolveAsync(
            cmd.InsuranceCaseId, null,
            CaseAudienceFlags.Customer | CaseAudienceFlags.AssignedStaff,
            ct);
        if (to.Count > 0)
        {
            var approvedGr = cmd.Decision == ApprovalStatus.Approved ? "Εγκρίθηκε" : "Απορρίφθηκε";
            var approvedEn = cmd.Decision == ApprovalStatus.Approved ? "approved" : "rejected";
            await notifications.DispatchAsync(new NotificationRequest(
                Kind: NotificationKind.InsuranceApprovalDecision,
                TitleGr: $"Έγκριση {caseRow.CaseNumber}: {approvedGr}",
                TitleEn: $"Approval {caseRow.CaseNumber}: {approvedEn}",
                BodyGr: cmd.Decision == ApprovalStatus.Approved
                    ? $"Εγκεκριμένο ποσό: {approval.ApprovedAmount:N2} €."
                    : $"Λόγος απόρριψης: {cmd.Data.Notes}",
                BodyEn: cmd.Decision == ApprovalStatus.Approved
                    ? $"Approved amount: {approval.ApprovedAmount:N2} €."
                    : $"Rejection reason: {cmd.Data.Notes}",
                Url: $"/cases/insurance/{cmd.InsuranceCaseId}",
                Recipients: to), ct);
        }
    }
}

public class InsurerDecideValidator : AbstractValidator<InsurerDecideCommand>
{
    public InsurerDecideValidator()
    {
        RuleFor(x => x.InsuranceCompanyId).NotEmpty();
        RuleFor(x => x.InsuranceCaseId).NotEmpty();
        RuleFor(x => x.Data.ApprovedAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Decision == ApprovalStatus.Approved)
            .WithMessage("ApprovedAmount must be >= 0 when approving.");
        RuleFor(x => x.Data.Notes)
            .NotEmpty().MaximumLength(1000)
            .When(x => x.Decision == ApprovalStatus.Rejected)
            .WithMessage("A reason is required when rejecting.");
    }
}
