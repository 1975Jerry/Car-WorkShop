using FluentValidation;
using Workshop.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Application.Features.Vehicles;

public record CreateVehicleCommand(VehicleUpsertDto Data) : IRequest<Guid>;

public class CreateVehicleHandler : IRequestHandler<CreateVehicleCommand, Guid>
{
    private readonly IWorkshopDbContext _db;
    public CreateVehicleHandler(IWorkshopDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateVehicleCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;

        var dupe = await _db.Vehicles.AnyAsync(v => !v.IsDeleted && v.PlateNumber == d.PlateNumber, ct);
        if (dupe) throw new InvalidOperationException($"Plate number {d.PlateNumber} already exists");

        var entity = new Vehicle
        {
            CustomerId = d.CustomerId,
            PlateNumber = d.PlateNumber,
            Vin = d.Vin,
            Brand = d.Brand,
            Model = d.Model,
            Version = d.Version,
            Year = d.Year,
            Color = d.Color,
            FuelType = d.FuelType,
            Mileage = d.Mileage,
            InsuranceCompanyId = d.InsuranceCompanyId,
            PolicyNumber = d.PolicyNumber,
            InsuranceExpiration = d.InsuranceExpiration,
            Notes = d.Notes,
            IsActive = d.IsActive
        };
        _db.Vehicles.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}

public record UpdateVehicleCommand(Guid Id, VehicleUpsertDto Data) : IRequest;

public class UpdateVehicleHandler : IRequestHandler<UpdateVehicleCommand>
{
    private readonly IWorkshopDbContext _db;
    public UpdateVehicleHandler(IWorkshopDbContext db) => _db = db;

    public async Task Handle(UpdateVehicleCommand cmd, CancellationToken ct)
    {
        var entity = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == cmd.Id && !v.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Vehicle {cmd.Id} not found");

        var d = cmd.Data;
        if (entity.PlateNumber != d.PlateNumber)
        {
            var dupe = await _db.Vehicles.AnyAsync(v =>
                !v.IsDeleted && v.Id != cmd.Id && v.PlateNumber == d.PlateNumber, ct);
            if (dupe) throw new InvalidOperationException($"Plate number {d.PlateNumber} already exists");
        }

        entity.CustomerId = d.CustomerId;
        entity.PlateNumber = d.PlateNumber;
        entity.Vin = d.Vin;
        entity.Brand = d.Brand;
        entity.Model = d.Model;
        entity.Version = d.Version;
        entity.Year = d.Year;
        entity.Color = d.Color;
        entity.FuelType = d.FuelType;
        entity.Mileage = d.Mileage;
        entity.InsuranceCompanyId = d.InsuranceCompanyId;
        entity.PolicyNumber = d.PolicyNumber;
        entity.InsuranceExpiration = d.InsuranceExpiration;
        entity.Notes = d.Notes;
        entity.IsActive = d.IsActive;

        await _db.SaveChangesAsync(ct);
    }
}

public record DeleteVehicleCommand(Guid Id) : IRequest;

public class DeleteVehicleHandler : IRequestHandler<DeleteVehicleCommand>
{
    private readonly IWorkshopDbContext _db;
    public DeleteVehicleHandler(IWorkshopDbContext db) => _db = db;

    public async Task Handle(DeleteVehicleCommand cmd, CancellationToken ct)
    {
        var entity = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == cmd.Id && !v.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Vehicle {cmd.Id} not found");
        entity.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }
}

public class CreateVehicleValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleValidator() => RuleFor(x => x.Data).SetValidator(new VehicleUpsertValidator());
}

public class UpdateVehicleValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new VehicleUpsertValidator());
    }
}

public class VehicleUpsertValidator : AbstractValidator<VehicleUpsertDto>
{
    public VehicleUpsertValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.PlateNumber)
            .NotEmpty()
            .MaximumLength(20);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Year)
            .InclusiveBetween(1900, DateTime.UtcNow.Year + 1)
            .When(x => x.Year.HasValue);
        RuleFor(x => x.Vin)
            .MaximumLength(30);
    }
}
