using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Customers;

public record UpdateCustomerCommand(Guid Id, CustomerUpsertDto Data) : IRequest;

public class UpdateCustomerHandler : IRequestHandler<UpdateCustomerCommand>
{
    private readonly IWorkshopDbContext _db;
    private readonly TimeProvider _clock;

    public UpdateCustomerHandler(IWorkshopDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Handle(UpdateCustomerCommand cmd, CancellationToken ct)
    {
        var entity = await _db.Customers.FirstOrDefaultAsync(c => c.Id == cmd.Id && !c.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Customer {cmd.Id} not found");

        var d = cmd.Data;
        entity.CustomerType = d.CustomerType;
        entity.FirstName = d.FirstName;
        entity.LastName = d.LastName;
        entity.CompanyName = d.CompanyName;
        entity.VatNumber = d.VatNumber;
        entity.TaxOffice = d.TaxOffice;
        entity.IdNumber = d.IdNumber;
        entity.MobilePhone = d.MobilePhone;
        entity.SecondaryPhone = d.SecondaryPhone;
        entity.Email = d.Email;
        entity.AddressLine = d.AddressLine;
        entity.City = d.City;
        entity.PostalCode = d.PostalCode;

        // Stamp GdprConsentAt only when consent transitions to true
        if (d.GdprConsent && !entity.GdprConsent)
            entity.GdprConsentAt = _clock.GetUtcNow().UtcDateTime;
        if (!d.GdprConsent)
            entity.GdprConsentAt = null;
        entity.GdprConsent = d.GdprConsent;

        entity.Notes = d.Notes;
        entity.IsActive = d.IsActive;

        await _db.SaveChangesAsync(ct);
    }
}

public class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.MobilePhone)
            .NotEmpty()
            .Matches(@"^[+]?[0-9 \-]{8,20}$");

        When(x => x.Data.CustomerType == CustomerType.Individual, () =>
        {
            RuleFor(x => x.Data.FirstName).NotEmpty();
            RuleFor(x => x.Data.LastName).NotEmpty();
        });
        When(x => x.Data.CustomerType == CustomerType.Company, () =>
        {
            RuleFor(x => x.Data.CompanyName).NotEmpty();
            RuleFor(x => x.Data.VatNumber).NotEmpty();
        });

        RuleFor(x => x.Data.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Email));
    }
}
