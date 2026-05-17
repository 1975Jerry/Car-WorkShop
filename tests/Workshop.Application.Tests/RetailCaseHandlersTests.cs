using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.RetailCases;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class RetailCaseHandlersTests
{
    internal static async Task<(Guid customerId, Guid vehicleId, Guid branchId)>
        SeedCustomerVehicleBranchAsync(Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var branch = new Branch { Name = "Athens", Code = "ATH", AddressLine = "x", City = "Athens", IsActive = true };
        var customer = new Customer
        {
            CustomerType = CustomerType.Individual,
            FirstName = "Maria", LastName = "Papadopoulou",
            MobilePhone = "6900000000",
            GdprConsent = true, GdprConsentAt = DateTime.UtcNow, IsActive = true
        };
        var vehicle = new Vehicle
        {
            Customer = customer, PlateNumber = "RET-1234",
            Brand = "VW", Model = "Polo", IsActive = true
        };
        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return (customer.Id, vehicle.Id, branch.Id);
    }

    [Fact]
    public async Task CreateRetailCase_GeneratesCaseNumberAndComputesTotal()
    {
        await using var db = TestDb.NewContext();
        var (custId, vehId, branchId) = await SeedCustomerVehicleBranchAsync(db);

        var handler = new CreateRetailCaseHandler(db, TimeProvider.System);
        var id = await handler.Handle(new CreateRetailCaseCommand(new RetailCaseUpsertDto(
            CustomerId: custId, VehicleId: vehId, BranchId: branchId,
            AssignedUserId: null, WorkType: "Φανοποιεία πλαϊνού",
            FinalCost: 400m, VatAmount: 96m,
            ScheduledDate: null, Notes: null)), default);

        var saved = await db.RetailCases.AsNoTracking().FirstAsync(c => c.Id == id);
        Assert.StartsWith($"RET-{DateTime.UtcNow.Year}-", saved.CaseNumber);
        Assert.Equal(496m, saved.TotalWithVat);
        Assert.Equal(RetailCaseStatus.Quoted, saved.Status);
    }

    [Fact]
    public async Task UpdateRetailCase_RecomputesTotal_AndRefusesIfClosed()
    {
        await using var db = TestDb.NewContext();
        var (custId, vehId, branchId) = await SeedCustomerVehicleBranchAsync(db);
        var handler = new CreateRetailCaseHandler(db, TimeProvider.System);
        var id = await handler.Handle(new CreateRetailCaseCommand(new RetailCaseUpsertDto(
            custId, vehId, branchId, null, "Polish", 100m, 24m, null, null)), default);

        await new UpdateRetailCaseHandler(db).Handle(new UpdateRetailCaseCommand(id,
            new RetailCaseUpsertDto(custId, vehId, branchId, null, "Polish + wax", 200m, 48m, null, null)), default);

        var saved = await db.RetailCases.AsNoTracking().FirstAsync(c => c.Id == id);
        Assert.Equal(248m, saved.TotalWithVat);

        // Mark closed in-place (tracked).
        var tracked = await db.RetailCases.FirstAsync(c => c.Id == id);
        tracked.Status = RetailCaseStatus.Closed;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateRetailCaseHandler(db).Handle(new UpdateRetailCaseCommand(id,
                new RetailCaseUpsertDto(custId, vehId, branchId, null, "After-close edit", 1m, 0m, null, null)), default));
    }

    [Fact]
    public void TransitionsTable_DefinesExpectedNeighbours()
    {
        Assert.True(RetailCaseTransitions.IsValid(RetailCaseStatus.Quoted, RetailCaseStatus.Accepted));
        Assert.True(RetailCaseTransitions.IsValid(RetailCaseStatus.Accepted, RetailCaseStatus.InProgress));
        Assert.True(RetailCaseTransitions.IsValid(RetailCaseStatus.InProgress, RetailCaseStatus.Completed));
        Assert.True(RetailCaseTransitions.IsValid(RetailCaseStatus.Completed, RetailCaseStatus.Paid));
        Assert.True(RetailCaseTransitions.IsValid(RetailCaseStatus.Paid, RetailCaseStatus.Closed));

        // Disallowed jumps.
        Assert.False(RetailCaseTransitions.IsValid(RetailCaseStatus.Quoted, RetailCaseStatus.InProgress));
        Assert.False(RetailCaseTransitions.IsValid(RetailCaseStatus.Completed, RetailCaseStatus.Closed));
        Assert.False(RetailCaseTransitions.IsValid(RetailCaseStatus.Closed, RetailCaseStatus.Quoted));

        // Same-state is rejected.
        Assert.False(RetailCaseTransitions.IsValid(RetailCaseStatus.Quoted, RetailCaseStatus.Quoted));
    }

    [Fact]
    public async Task Transition_AcceptThenStart_BlocksWithoutSchedule()
    {
        await using var db = TestDb.NewContext();
        var (custId, vehId, branchId) = await SeedCustomerVehicleBranchAsync(db);
        var id = await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                custId, vehId, branchId, null, "Job", 100m, 24m, null, null)), default);

        var user = new TestCurrentUser();
        var t = new TransitionRetailCaseHandler(db, user, TimeProvider.System);
        await t.Handle(new TransitionRetailCaseCommand(id, RetailCaseStatus.Accepted, null), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            t.Handle(new TransitionRetailCaseCommand(id, RetailCaseStatus.InProgress, null), default));
        Assert.Contains("repair must be scheduled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transition_RecordsCaseEvent()
    {
        await using var db = TestDb.NewContext();
        var (custId, vehId, branchId) = await SeedCustomerVehicleBranchAsync(db);
        var id = await new CreateRetailCaseHandler(db, TimeProvider.System).Handle(
            new CreateRetailCaseCommand(new RetailCaseUpsertDto(
                custId, vehId, branchId, null, "Job", 100m, 24m, null, null)), default);

        var user = new TestCurrentUser();
        await new TransitionRetailCaseHandler(db, user, TimeProvider.System).Handle(
            new TransitionRetailCaseCommand(id, RetailCaseStatus.Accepted, "Customer agreed"), default);

        var events = await db.CaseEvents.AsNoTracking()
            .Where(e => e.RetailCaseId == id).ToListAsync();
        var e = Assert.Single(events);
        Assert.Equal("Quoted", e.FromStatus);
        Assert.Equal("Accepted", e.ToStatus);
        Assert.Equal("Customer agreed", e.Reason);
        Assert.Equal(user.UserId, e.TriggeredById);
    }

    [Fact]
    public async Task Validators_RejectEmptyAndBadValues()
    {
        var v = new RetailCaseUpsertValidator();
        var empty = new RetailCaseUpsertDto(Guid.Empty, Guid.Empty, Guid.Empty, null, "", -1m, -1m, null, null);
        var result = await v.ValidateAsync(empty);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RetailCaseUpsertDto.WorkType));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RetailCaseUpsertDto.FinalCost));
    }
}
