using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Insurance;

namespace Workshop.Application.Features.Payments;

public record CreatePaymentCommand(Guid InsuranceCaseId, CreatePaymentDto Data) : IRequest<Guid>;

public class CreatePaymentHandler(IWorkshopDbContext db)
    : IRequestHandler<CreatePaymentCommand, Guid>
{
    public async Task<Guid> Handle(CreatePaymentCommand cmd, CancellationToken ct)
    {
        var caseExists = await db.InsuranceCases.AsNoTracking()
            .AnyAsync(c => c.Id == cmd.InsuranceCaseId, ct);
        if (!caseExists)
            throw new KeyNotFoundException($"Insurance case {cmd.InsuranceCaseId} not found");

        var d = cmd.Data;
        var entity = new Payment
        {
            InsuranceCaseId = cmd.InsuranceCaseId,
            Amount = d.Amount,
            PaymentDate = d.PaymentDate,
            PaymentMethod = d.PaymentMethod,
            Payer = d.Payer,
            ReferenceNumber = d.ReferenceNumber,
            Notes = d.Notes
        };
        db.Payments.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record DeletePaymentCommand(Guid PaymentId) : IRequest;

public class DeletePaymentHandler(IWorkshopDbContext db)
    : IRequestHandler<DeletePaymentCommand>
{
    public async Task Handle(DeletePaymentCommand cmd, CancellationToken ct)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == cmd.PaymentId, ct)
            ?? throw new KeyNotFoundException($"Payment {cmd.PaymentId} not found");
        db.Payments.Remove(payment);
        await db.SaveChangesAsync(ct);
    }
}

public record CreateRetailPaymentCommand(Guid RetailCaseId, CreatePaymentDto Data) : IRequest<Guid>;

public class CreateRetailPaymentHandler(IWorkshopDbContext db)
    : IRequestHandler<CreateRetailPaymentCommand, Guid>
{
    public async Task<Guid> Handle(CreateRetailPaymentCommand cmd, CancellationToken ct)
    {
        var caseExists = await db.RetailCases.AsNoTracking()
            .AnyAsync(c => c.Id == cmd.RetailCaseId, ct);
        if (!caseExists)
            throw new KeyNotFoundException($"Retail case {cmd.RetailCaseId} not found");

        var d = cmd.Data;
        var entity = new Payment
        {
            RetailCaseId = cmd.RetailCaseId,
            Amount = d.Amount,
            PaymentDate = d.PaymentDate,
            PaymentMethod = d.PaymentMethod,
            Payer = d.Payer,
            ReferenceNumber = d.ReferenceNumber,
            Notes = d.Notes
        };
        db.Payments.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public class CreatePaymentValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentValidator()
    {
        RuleFor(x => x.InsuranceCaseId).NotEmpty();
        RuleFor(x => x.Data.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero.");
        RuleFor(x => x.Data.PaymentDate).NotEmpty();
    }
}

public class CreateRetailPaymentValidator : AbstractValidator<CreateRetailPaymentCommand>
{
    public CreateRetailPaymentValidator()
    {
        RuleFor(x => x.RetailCaseId).NotEmpty();
        RuleFor(x => x.Data.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero.");
        RuleFor(x => x.Data.PaymentDate).NotEmpty();
    }
}
