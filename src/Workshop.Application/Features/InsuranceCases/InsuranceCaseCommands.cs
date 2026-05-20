using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsuranceCases;

public record CreateInsuranceCaseCommand(InsuranceCaseUpsertDto Data) : IRequest<Guid>;

public class CreateInsuranceCaseHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<CreateInsuranceCaseCommand, Guid>
{
    public async Task<Guid> Handle(CreateInsuranceCaseCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;

        // Generate case number: INS-YYYY-NNNN where NNNN is the count + 1 for the year
        var year = clock.GetUtcNow().Year;
        var prefix = $"INS-{year}-";
        var yearCount = await db.InsuranceCases
            .Where(c => c.CaseNumber.StartsWith(prefix))
            .CountAsync(ct);
        var caseNumber = $"{prefix}{(yearCount + 1).ToString("D4")}";

        // Ensure unique (defensive — race condition)
        while (await db.InsuranceCases.AnyAsync(c => c.CaseNumber == caseNumber, ct))
        {
            yearCount++;
            caseNumber = $"{prefix}{(yearCount + 1).ToString("D4")}";
        }

        var entity = new InsuranceCase
        {
            CaseNumber = caseNumber,
            CustomerId = d.CustomerId,
            VehicleId = d.VehicleId,
            BranchId = d.BranchId,
            InsuranceCompanyId = d.InsuranceCompanyId,
            ClaimNumber = d.ClaimNumber,
            Priority = d.Priority,
            AssessorId = d.AssessorId,
            AdjusterId = d.AdjusterId,
            AssignedUserId = d.AssignedUserId,
            DriverFirstName = d.DriverFirstName,
            DriverLastName = d.DriverLastName,
            DriverPhone = d.DriverPhone,
            DriverEmail = d.DriverEmail,
            AccidentDate = d.AccidentDate,
            MileageAtAssessment = d.MileageAtAssessment,
            Notes = d.Notes,
            Status = InsuranceCaseStatus.NewCase
        };
        db.InsuranceCases.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateInsuranceCaseCommand(Guid Id, InsuranceCaseUpsertDto Data) : IRequest;

public class UpdateInsuranceCaseHandler(IWorkshopDbContext db) : IRequestHandler<UpdateInsuranceCaseCommand>
{
    public async Task Handle(UpdateInsuranceCaseCommand cmd, CancellationToken ct)
    {
        var entity = await db.InsuranceCases.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Case {cmd.Id} not found");
        var d = cmd.Data;

        entity.CustomerId = d.CustomerId;
        entity.VehicleId = d.VehicleId;
        entity.BranchId = d.BranchId;
        entity.InsuranceCompanyId = d.InsuranceCompanyId;
        entity.ClaimNumber = d.ClaimNumber;
        entity.Priority = d.Priority;
        entity.AssessorId = d.AssessorId;
        entity.AdjusterId = d.AdjusterId;
        entity.AssignedUserId = d.AssignedUserId;
        entity.DriverFirstName = d.DriverFirstName;
        entity.DriverLastName = d.DriverLastName;
        entity.DriverPhone = d.DriverPhone;
        entity.DriverEmail = d.DriverEmail;
        entity.AccidentDate = d.AccidentDate;
        entity.MileageAtAssessment = d.MileageAtAssessment;
        entity.Notes = d.Notes;

        await db.SaveChangesAsync(ct);
    }
}

public class InsuranceCaseUpsertValidator : AbstractValidator<InsuranceCaseUpsertDto>
{
    public InsuranceCaseUpsertValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.InsuranceCompanyId).NotEmpty();
        RuleFor(x => x.DriverEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.DriverEmail));
    }
}

public class CreateInsuranceCaseValidator : AbstractValidator<CreateInsuranceCaseCommand>
{ public CreateInsuranceCaseValidator() => RuleFor(x => x.Data).SetValidator(new InsuranceCaseUpsertValidator()); }

public class UpdateInsuranceCaseValidator : AbstractValidator<UpdateInsuranceCaseCommand>
{
    public UpdateInsuranceCaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new InsuranceCaseUpsertValidator());
    }
}
