using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Notifications;
using Workshop.Application.Features.InsuranceParts;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.SupplierPortal;

/// <summary>
/// Supplier-side state advance. Allowed targets are Ordered, InTransit, Cancelled.
/// Received is refused — physical receipt is confirmed workshop-side.
/// Strictly scoped: the part line's SupplierId must equal the caller's SupplierId.
/// Works for both insurance and retail part lines.
/// </summary>
public record SupplierDispatchCommand(
    Guid SupplierId,
    Guid LineId,
    SupplierLineKind Kind,
    PartReceivedStatus TargetStatus,
    string? Notes = null) : IRequest;

public class SupplierDispatchHandler(
    IWorkshopDbContext db,
    TimeProvider clock,
    INotificationDispatcher notifications,
    ICaseNotificationRecipients recipients)
    : IRequestHandler<SupplierDispatchCommand>
{
    public async Task Handle(SupplierDispatchCommand cmd, CancellationToken ct)
    {
        if (cmd.TargetStatus == PartReceivedStatus.Received)
            throw new InvalidOperationException(
                "Receipt is confirmed by the workshop, not the supplier.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        Guid? insuranceCaseId = null;
        Guid? retailCaseId = null;
        string partName = string.Empty;
        string caseNumber = string.Empty;

        if (cmd.Kind == SupplierLineKind.Insurance)
        {
            var line = await db.InsurancePartLines.FirstOrDefaultAsync(p => p.Id == cmd.LineId, ct)
                ?? throw new KeyNotFoundException($"Insurance part line {cmd.LineId} not found");
            if (line.SupplierId != cmd.SupplierId)
                throw new UnauthorizedAccessException(
                    "This part line does not belong to the calling supplier.");
            if (!UpdatePartReceivedStatusHandler.IsValidTransition(line.ReceivedStatus, cmd.TargetStatus))
                throw new InvalidOperationException(
                    $"Cannot transition from '{line.ReceivedStatus}' to '{cmd.TargetStatus}'.");

            line.ReceivedStatus = cmd.TargetStatus;
            switch (cmd.TargetStatus)
            {
                case PartReceivedStatus.Ordered:
                    line.Ordered = true;
                    line.OrderDate ??= today;
                    break;
                case PartReceivedStatus.InTransit:
                    line.Ordered = true;
                    line.OrderDate ??= today;
                    break;
                case PartReceivedStatus.Cancelled:
                    line.Ordered = false;
                    break;
            }
            if (!string.IsNullOrWhiteSpace(cmd.Notes)) line.Notes = cmd.Notes;

            var meta = await db.InsurancePartLines.AsNoTracking()
                .Where(p => p.Id == cmd.LineId)
                .Select(p => new { p.PartName, CaseId = p.Assessment.InsuranceCaseId, p.Assessment.InsuranceCase.CaseNumber })
                .FirstAsync(ct);
            insuranceCaseId = meta.CaseId;
            partName = meta.PartName;
            caseNumber = meta.CaseNumber;
        }
        else
        {
            var line = await db.RetailPartLines.FirstOrDefaultAsync(p => p.Id == cmd.LineId, ct)
                ?? throw new KeyNotFoundException($"Retail part line {cmd.LineId} not found");
            if (line.SupplierId != cmd.SupplierId)
                throw new UnauthorizedAccessException(
                    "This part line does not belong to the calling supplier.");
            if (!UpdatePartReceivedStatusHandler.IsValidTransition(line.ReceivedStatus, cmd.TargetStatus))
                throw new InvalidOperationException(
                    $"Cannot transition from '{line.ReceivedStatus}' to '{cmd.TargetStatus}'.");

            line.ReceivedStatus = cmd.TargetStatus;
            if (!string.IsNullOrWhiteSpace(cmd.Notes)) line.Notes = cmd.Notes;

            retailCaseId = line.RetailCaseId;
            partName = line.PartName;
            caseNumber = await db.RetailCases.AsNoTracking()
                .Where(c => c.Id == line.RetailCaseId)
                .Select(c => c.CaseNumber)
                .FirstAsync(ct);
        }

        await db.SaveChangesAsync(ct);

        var to = await recipients.ResolveAsync(
            insuranceCaseId, retailCaseId,
            CaseAudienceFlags.AssignedStaff,
            ct);
        if (to.Count > 0)
        {
            var url = insuranceCaseId is { } iid
                ? $"/cases/insurance/{iid}"
                : $"/cases/retail/{retailCaseId!.Value}";
            await notifications.DispatchAsync(new NotificationRequest(
                Kind: NotificationKind.SupplierDispatch,
                TitleGr: $"Ανταλλακτικό {caseNumber}: {StatusLabelGr(cmd.TargetStatus)}",
                TitleEn: $"Part {caseNumber}: {cmd.TargetStatus}",
                BodyGr: $"«{partName}»: ο προμηθευτής δήλωσε {StatusLabelGr(cmd.TargetStatus)}.",
                BodyEn: $"\"{partName}\": supplier marked {cmd.TargetStatus}.",
                Url: url,
                Recipients: to), ct);
        }
    }

    private static string StatusLabelGr(PartReceivedStatus s) => s switch
    {
        PartReceivedStatus.Pending => "Εκκρεμεί",
        PartReceivedStatus.Ordered => "Παραγγέλθηκε",
        PartReceivedStatus.InTransit => "Σε Μεταφορά",
        PartReceivedStatus.Received => "Παραλήφθηκε",
        PartReceivedStatus.Defective => "Ελαττωματικό",
        PartReceivedStatus.Cancelled => "Ακυρώθηκε",
        _ => s.ToString(),
    };
}

public class SupplierDispatchValidator : AbstractValidator<SupplierDispatchCommand>
{
    public SupplierDispatchValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.TargetStatus).Must(s => s != PartReceivedStatus.Received)
            .WithMessage("Receipt is confirmed by the workshop, not the supplier.");
        RuleFor(x => x.Notes).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
