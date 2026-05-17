using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Customers;
using Workshop.Application.Features.Vehicles;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class VehicleHandlersTests
{
    private static CustomerUpsertDto IndividualDto() =>
        new(CustomerType.Individual, "Test", "Owner", null, null, null, null,
            MobilePhone: "6900000000", null, null, null, null, null,
            GdprConsent: true, null, IsActive: true);

    private static VehicleUpsertDto VehicleDto(Guid customerId, string plate = "ABC-1234") =>
        new(customerId, plate, "WBA12345678", "BMW", "320d", "M-Sport",
            Year: 2020, Color: "Black", FuelType: Workshop.Domain.Enums.FuelType.Diesel,
            Mileage: 50000, InsuranceCompanyId: null, PolicyNumber: null,
            InsuranceExpiration: null, Notes: null, IsActive: true);

    private static async Task<Guid> SeedCustomerAsync(Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var handler = new CreateCustomerHandler(db, TimeProvider.System);
        return await handler.Handle(new CreateCustomerCommand(IndividualDto()), default);
    }

    [Fact]
    public async Task Create_PersistsVehicleLinkedToCustomer()
    {
        await using var db = TestDb.NewContext();
        var customerId = await SeedCustomerAsync(db);

        var id = await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerId)), default);

        var saved = await db.Vehicles.FirstAsync(v => v.Id == id);
        Assert.Equal(customerId, saved.CustomerId);
        Assert.Equal("ABC-1234", saved.PlateNumber);
        Assert.Equal("BMW", saved.Brand);
    }

    [Fact]
    public async Task Create_DuplicatePlate_Throws()
    {
        await using var db = TestDb.NewContext();
        var customerId = await SeedCustomerAsync(db);

        await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerId, "DUPE-001")), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateVehicleHandler(db).Handle(
                new CreateVehicleCommand(VehicleDto(customerId, "DUPE-001")), default));
        Assert.Contains("DUPE-001", ex.Message);
    }

    [Fact]
    public async Task Update_AllowsChangingPlate_WhenNotInUse()
    {
        await using var db = TestDb.NewContext();
        var customerId = await SeedCustomerAsync(db);
        var id = await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerId, "OLD-001")), default);

        var updatedDto = VehicleDto(customerId, "NEW-001");
        await new UpdateVehicleHandler(db)
            .Handle(new UpdateVehicleCommand(id, updatedDto), default);

        var saved = await db.Vehicles.AsNoTracking().FirstAsync(v => v.Id == id);
        Assert.Equal("NEW-001", saved.PlateNumber);
    }

    [Fact]
    public async Task Update_RejectsPlateAlreadyOwnedByAnotherVehicle()
    {
        await using var db = TestDb.NewContext();
        var customerId = await SeedCustomerAsync(db);

        var idA = await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerId, "PLATE-A")), default);
        var idB = await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerId, "PLATE-B")), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateVehicleHandler(db).Handle(
                new UpdateVehicleCommand(idB, VehicleDto(customerId, "PLATE-A")), default));
        Assert.Contains("PLATE-A", ex.Message);
    }

    [Fact]
    public async Task Validator_RejectsImpossibleYear()
    {
        var validator = new VehicleUpsertValidator();
        var dto = VehicleDto(Guid.NewGuid()) with { Year = 1800 };

        var result = await validator.ValidateAsync(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("Year"));
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndExcludesFromList()
    {
        await using var db = TestDb.NewContext();
        var customerId = await SeedCustomerAsync(db);
        var id = await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerId)), default);

        await new DeleteVehicleHandler(db).Handle(new DeleteVehicleCommand(id), default);

        var page = await new ListVehiclesHandler(db).Handle(new ListVehiclesQuery(), default);
        Assert.DoesNotContain(page.Items, v => v.Id == id);
    }

    [Fact]
    public async Task List_FiltersByCustomerId()
    {
        await using var db = TestDb.NewContext();
        var customerA = await SeedCustomerAsync(db);
        var customerB = await SeedCustomerAsync(db);

        await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerA, "A1-111")), default);
        await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerA, "A2-222")), default);
        await new CreateVehicleHandler(db)
            .Handle(new CreateVehicleCommand(VehicleDto(customerB, "B1-333")), default);

        var aOnly = await new ListVehiclesHandler(db)
            .Handle(new ListVehiclesQuery(CustomerId: customerA), default);
        Assert.Equal(2, aOnly.TotalCount);
        Assert.All(aOnly.Items, v => Assert.Equal(customerA, v.CustomerId));
    }
}
