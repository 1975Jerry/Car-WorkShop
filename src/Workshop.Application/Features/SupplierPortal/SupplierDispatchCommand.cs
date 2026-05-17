using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
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

public class SupplierDispatchHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<SupplierDispatchCommand>
{
    public async Task Handle(SupplierDispatchCommand cmd, CancellationToken ct)
    {
        if (cmd.TargetStatus == PartReceivedStatus.Received)
            throw new InvalidOperationException(
                "Receipt is confirmed by the workshop, not the supplier.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

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
        }

        await db.SaveChangesAsync(ct);
    }
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
