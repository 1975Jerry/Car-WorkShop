using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Features.InsuranceParts;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.RetailParts;

public record CreateRetailPartLineCommand(Guid RetailCaseId, RetailPartLineUpsertDto Data) : IRequest<Guid>;

public class CreateRetailPartLineHandler(IWorkshopDbContext db)
    : IRequestHandler<CreateRetailPartLineCommand, Guid>
{
    public async Task<Guid> Handle(CreateRetailPartLineCommand cmd, CancellationToken ct)
    {
        var caseExists = await db.RetailCases.AsNoTracking()
            .AnyAsync(c => c.Id == cmd.RetailCaseId, ct);
        if (!caseExists)
            throw new KeyNotFoundException($"Retail case {cmd.RetailCaseId} not found");

        var d = cmd.Data;
        var entity = new RetailPartLine
        {
            RetailCaseId = cmd.RetailCaseId,
            SupplierId = d.SupplierId,
            DestinationBranchId = d.DestinationBranchId,
            PartType = d.PartType,
            PartName = d.PartName,
            Quantity = d.Quantity,
            UnitCost = d.UnitCost,
            Total = InsurancePartLineCalculator.Total(d.Quantity, d.UnitCost, null),
            ReceivedStatus = PartReceivedStatus.Pending,
            Notes = d.Notes
        };
        db.RetailPartLines.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateRetailPartLineCommand(Guid Id, RetailPartLineUpsertDto Data) : IRequest;

public class UpdateRetailPartLineHandler(IWorkshopDbContext db)
    : IRequestHandler<UpdateRetailPartLineCommand>
{
    public async Task Handle(UpdateRetailPartLineCommand cmd, CancellationToken ct)
    {
        var entity = await db.RetailPartLines.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Retail part line {cmd.Id} not found");

        var d = cmd.Data;
        entity.SupplierId = d.SupplierId;
        entity.DestinationBranchId = d.DestinationBranchId;
        entity.PartType = d.PartType;
        entity.PartName = d.PartName;
        entity.Quantity = d.Quantity;
        entity.UnitCost = d.UnitCost;
        entity.Total = InsurancePartLineCalculator.Total(d.Quantity, d.UnitCost, null);
        entity.Notes = d.Notes;

        await db.SaveChangesAsync(ct);
    }
}

public record DeleteRetailPartLineCommand(Guid Id) : IRequest;

public class DeleteRetailPartLineHandler(IWorkshopDbContext db)
    : IRequestHandler<DeleteRetailPartLineCommand>
{
    public async Task Handle(DeleteRetailPartLineCommand cmd, CancellationToken ct)
    {
        var entity = await db.RetailPartLines.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Retail part line {cmd.Id} not found");
        if (entity.ReceivedStatus == PartReceivedStatus.Received)
            throw new InvalidOperationException("Cannot delete a part line that has been received. Mark Defective or Cancelled instead.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}

public record UpdateRetailPartReceivedStatusCommand(
    Guid PartLineId,
    PartReceivedStatus TargetStatus,
    Guid? WarehouseId = null,
    string? StorageLocation = null) : IRequest;

public class UpdateRetailPartReceivedStatusHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<UpdateRetailPartReceivedStatusCommand>
{
    public async Task Handle(UpdateRetailPartReceivedStatusCommand cmd, CancellationToken ct)
    {
        var entity = await db.RetailPartLines.FirstOrDefaultAsync(p => p.Id == cmd.PartLineId, ct)
            ?? throw new KeyNotFoundException($"Retail part line {cmd.PartLineId} not found");

        // Reuse the insurance-side transition rules — they're identical.
        if (!UpdatePartReceivedStatusHandler.IsValidTransition(entity.ReceivedStatus, cmd.TargetStatus))
            throw new InvalidOperationException(
                $"Cannot transition part line from '{entity.ReceivedStatus}' to '{cmd.TargetStatus}'.");

        if (cmd.TargetStatus == PartReceivedStatus.Received && cmd.WarehouseId is null)
            throw new InvalidOperationException("Warehouse must be specified when marking a part as Received.");

        entity.ReceivedStatus = cmd.TargetStatus;
        switch (cmd.TargetStatus)
        {
            case PartReceivedStatus.Received:
                entity.WarehouseId = cmd.WarehouseId;
                if (!string.IsNullOrWhiteSpace(cmd.StorageLocation))
                    entity.StorageLocation = cmd.StorageLocation;
                break;
            case PartReceivedStatus.Cancelled:
                entity.WarehouseId = null;
                entity.StorageLocation = null;
                break;
        }

        _ = clock; // not currently needed; reserved for future date stamps
        await db.SaveChangesAsync(ct);
    }
}

public class RetailPartLineUpsertValidator : AbstractValidator<RetailPartLineUpsertDto>
{
    public RetailPartLineUpsertValidator()
    {
        RuleFor(x => x.DestinationBranchId).NotEmpty();
        RuleFor(x => x.PartName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public class CreateRetailPartLineValidator : AbstractValidator<CreateRetailPartLineCommand>
{
    public CreateRetailPartLineValidator()
    {
        RuleFor(x => x.RetailCaseId).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new RetailPartLineUpsertValidator());
    }
}

public class UpdateRetailPartLineValidator : AbstractValidator<UpdateRetailPartLineCommand>
{
    public UpdateRetailPartLineValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new RetailPartLineUpsertValidator());
    }
}

public class UpdateRetailPartReceivedStatusValidator : AbstractValidator<UpdateRetailPartReceivedStatusCommand>
{
    public UpdateRetailPartReceivedStatusValidator()
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
