using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsuranceParts;

public record CreateInsurancePartLineCommand(
    Guid InsuranceCaseId,
    InsurancePartLineUpsertDto Data) : IRequest<Guid>;

public class CreateInsurancePartLineHandler(IWorkshopDbContext db)
    : IRequestHandler<CreateInsurancePartLineCommand, Guid>
{
    public async Task<Guid> Handle(CreateInsurancePartLineCommand cmd, CancellationToken ct)
    {
        var assessmentId = await db.Assessments.AsNoTracking()
            .Where(a => a.InsuranceCaseId == cmd.InsuranceCaseId)
            .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);

        if (assessmentId is null)
            throw new InvalidOperationException(
                "Cannot add a part line before the Assessment record exists for this case.");

        var d = cmd.Data;
        var entity = new InsurancePartLine
        {
            AssessmentId = assessmentId.Value,
            SupplierId = d.SupplierId,
            DestinationBranchId = d.DestinationBranchId,
            PartType = d.PartType,
            PartName = d.PartName,
            Quantity = d.Quantity,
            UnitCost = d.UnitCost,
            DiscountPct = d.DiscountPct,
            Total = InsurancePartLineCalculator.Total(d.Quantity, d.UnitCost, d.DiscountPct),
            AvailabilityStatus = d.AvailabilityStatus,
            InsuranceApproved = d.InsuranceApproved,
            Ordered = false,
            ReceivedStatus = PartReceivedStatus.Pending,
            Notes = d.Notes
        };
        db.InsurancePartLines.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateInsurancePartLineCommand(Guid Id, InsurancePartLineUpsertDto Data) : IRequest;

public class UpdateInsurancePartLineHandler(IWorkshopDbContext db)
    : IRequestHandler<UpdateInsurancePartLineCommand>
{
    public async Task Handle(UpdateInsurancePartLineCommand cmd, CancellationToken ct)
    {
        var entity = await db.InsurancePartLines.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Part line {cmd.Id} not found");

        var d = cmd.Data;
        entity.SupplierId = d.SupplierId;
        entity.DestinationBranchId = d.DestinationBranchId;
        entity.PartType = d.PartType;
        entity.PartName = d.PartName;
        entity.Quantity = d.Quantity;
        entity.UnitCost = d.UnitCost;
        entity.DiscountPct = d.DiscountPct;
        entity.Total = InsurancePartLineCalculator.Total(d.Quantity, d.UnitCost, d.DiscountPct);
        entity.AvailabilityStatus = d.AvailabilityStatus;
        entity.InsuranceApproved = d.InsuranceApproved;
        entity.Notes = d.Notes;

        await db.SaveChangesAsync(ct);
    }
}

public record DeleteInsurancePartLineCommand(Guid Id) : IRequest;

public class DeleteInsurancePartLineHandler(IWorkshopDbContext db)
    : IRequestHandler<DeleteInsurancePartLineCommand>
{
    public async Task Handle(DeleteInsurancePartLineCommand cmd, CancellationToken ct)
    {
        var entity = await db.InsurancePartLines.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Part line {cmd.Id} not found");
        if (entity.ReceivedStatus == PartReceivedStatus.Received)
            throw new InvalidOperationException("Cannot delete a part line that has been received. Mark Defective or Cancelled instead.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}

public class InsurancePartLineUpsertValidator : AbstractValidator<InsurancePartLineUpsertDto>
{
    public InsurancePartLineUpsertValidator()
    {
        RuleFor(x => x.DestinationBranchId).NotEmpty();
        RuleFor(x => x.PartName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountPct).InclusiveBetween(0, 100).When(x => x.DiscountPct.HasValue);
    }
}

public class CreateInsurancePartLineValidator : AbstractValidator<CreateInsurancePartLineCommand>
{
    public CreateInsurancePartLineValidator()
    {
        RuleFor(x => x.InsuranceCaseId).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new InsurancePartLineUpsertValidator());
    }
}

public class UpdateInsurancePartLineValidator : AbstractValidator<UpdateInsurancePartLineCommand>
{
    public UpdateInsurancePartLineValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new InsurancePartLineUpsertValidator());
    }
}
