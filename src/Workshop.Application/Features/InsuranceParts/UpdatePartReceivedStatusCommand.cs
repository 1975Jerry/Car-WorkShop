using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsuranceParts;

public record UpdatePartReceivedStatusCommand(
    Guid PartLineId,
    PartReceivedStatus TargetStatus,
    Guid? WarehouseId = null,
    string? StorageLocation = null,
    DateOnly? OrderDate = null) : IRequest;

public class UpdatePartReceivedStatusHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<UpdatePartReceivedStatusCommand>
{
    public async Task Handle(UpdatePartReceivedStatusCommand cmd, CancellationToken ct)
    {
        var entity = await db.InsurancePartLines.FirstOrDefaultAsync(p => p.Id == cmd.PartLineId, ct)
            ?? throw new KeyNotFoundException($"Part line {cmd.PartLineId} not found");

        if (!IsValidTransition(entity.ReceivedStatus, cmd.TargetStatus))
            throw new InvalidOperationException(
                $"Cannot transition part line from '{entity.ReceivedStatus}' to '{cmd.TargetStatus}'.");

        // Received requires a Warehouse.
        if (cmd.TargetStatus == PartReceivedStatus.Received && cmd.WarehouseId is null)
            throw new InvalidOperationException("Warehouse must be specified when marking a part as Received.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        entity.ReceivedStatus = cmd.TargetStatus;
        switch (cmd.TargetStatus)
        {
            case PartReceivedStatus.Ordered:
                entity.Ordered = true;
                entity.OrderDate ??= cmd.OrderDate ?? today;
                break;
            case PartReceivedStatus.InTransit:
                entity.Ordered = true;
                entity.OrderDate ??= cmd.OrderDate ?? today;
                break;
            case PartReceivedStatus.Received:
                entity.Ordered = true;
                entity.WarehouseId = cmd.WarehouseId;
                if (!string.IsNullOrWhiteSpace(cmd.StorageLocation))
                    entity.StorageLocation = cmd.StorageLocation;
                entity.ReceivedDate = today;
                break;
            case PartReceivedStatus.Defective:
                // Keep WarehouseId/StorageLocation — the defective part still occupies
                // a slot until the supplier picks it up.
                break;
            case PartReceivedStatus.Cancelled:
                entity.Ordered = false;
                entity.WarehouseId = null;
                entity.StorageLocation = null;
                break;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Allowed transitions:
    ///   Pending → Ordered, Cancelled
    ///   Ordered → InTransit, Received, Cancelled
    ///   InTransit → Received, Cancelled
    ///   Received → Defective
    ///   Defective → Received (after replacement), Cancelled
    ///   Cancelled → (terminal)
    ///
    /// Also: a part may always be "kept at" its current status (idempotent no-op
    /// requests are accepted) so the UI can safely re-trigger.
    /// </summary>
    public static bool IsValidTransition(PartReceivedStatus from, PartReceivedStatus to)
    {
        if (from == to) return true;
        return (from, to) switch
        {
            (PartReceivedStatus.Pending, PartReceivedStatus.Ordered) => true,
            (PartReceivedStatus.Pending, PartReceivedStatus.Cancelled) => true,
            (PartReceivedStatus.Ordered, PartReceivedStatus.InTransit) => true,
            (PartReceivedStatus.Ordered, PartReceivedStatus.Received) => true,
            (PartReceivedStatus.Ordered, PartReceivedStatus.Cancelled) => true,
            (PartReceivedStatus.InTransit, PartReceivedStatus.Received) => true,
            (PartReceivedStatus.InTransit, PartReceivedStatus.Cancelled) => true,
            (PartReceivedStatus.Received, PartReceivedStatus.Defective) => true,
            (PartReceivedStatus.Defective, PartReceivedStatus.Received) => true,
            (PartReceivedStatus.Defective, PartReceivedStatus.Cancelled) => true,
            _ => false
        };
    }

    public static IReadOnlyList<PartReceivedStatus> AllowedNext(PartReceivedStatus current) =>
        Enum.GetValues<PartReceivedStatus>()
            .Where(s => s != current && IsValidTransition(current, s))
            .ToList();
}

public class UpdatePartReceivedStatusValidator : AbstractValidator<UpdatePartReceivedStatusCommand>
{
    public UpdatePartReceivedStatusValidator()
    {
        RuleFor(x => x.PartLineId).NotEmpty();
        RuleFor(x => x.WarehouseId)
            .NotNull()
            .When(x => x.TargetStatus == PartReceivedStatus.Received)
            .WithMessage("WarehouseId is required when marking the part as Received.");
        RuleFor(x => x.StorageLocation)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.StorageLocation));
    }
}
