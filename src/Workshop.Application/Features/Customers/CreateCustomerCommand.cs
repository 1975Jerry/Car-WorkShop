using FluentValidation;
using MediatR;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Customers;

public record CreateCustomerCommand(CustomerUpsertDto Data) : IRequest<Guid>;

public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, Guid>
{
    private readonly IWorkshopDbContext _db;
    private readonly TimeProvider _clock;

    public CreateCustomerHandler(IWorkshopDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(CreateCustomerCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        var entity = new Customer
        {
            CustomerType = d.CustomerType,
            FirstName = d.FirstName,
            LastName = d.LastName,
            CompanyName = d.CompanyName,
            VatNumber = d.VatNumber,
            TaxOffice = d.TaxOffice,
            IdNumber = d.IdNumber,
            MobilePhone = d.MobilePhone,
            SecondaryPhone = d.SecondaryPhone,
            Email = d.Email,
            AddressLine = d.AddressLine,
            City = d.City,
            PostalCode = d.PostalCode,
            GdprConsent = d.GdprConsent,
            GdprConsentAt = d.GdprConsent ? _clock.GetUtcNow().UtcDateTime : null,
            Notes = d.Notes,
            IsActive = d.IsActive
        };
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.MobilePhone)
            .NotEmpty()
            .Matches(@"^[+]?[0-9 \-]{8,20}$")
            .WithMessage("Invalid phone format");

        When(x => x.Data.CustomerType == CustomerType.Individual, () =>
        {
            RuleFor(x => x.Data.FirstName).NotEmpty().WithMessage("First name is required for individuals");
            RuleFor(x => x.Data.LastName).NotEmpty().WithMessage("Last name is required for individuals");
        });

        When(x => x.Data.CustomerType == CustomerType.Company, () =>
        {
            RuleFor(x => x.Data.CompanyName).NotEmpty().WithMessage("Company name is required for companies");
            RuleFor(x => x.Data.VatNumber).NotEmpty().WithMessage("VAT number is required for companies");
        });

        RuleFor(x => x.Data.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Email));
    }
}
