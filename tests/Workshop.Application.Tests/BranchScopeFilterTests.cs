using Microsoft.EntityFrameworkCore;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class BranchScopeFilterTests
{
    [Fact]
    public async Task InsuranceCases_Are_Scoped_To_Branch_For_Non_Admin_User()
    {
        var dbName = Guid.NewGuid().ToString();
        var (branchA, branchB) = await SeedTwoBranchCasesAsync(dbName);

        await using var dbBranchA = TestDb.NewContext(dbName, new TestCurrentUser(branchA));
        var visible = dbBranchA.InsuranceCases.Select(c => c.BranchId).ToList();

        Assert.Single(visible);
        Assert.Equal(branchA, visible[0]);
    }

    [Fact]
    public async Task Admin_Sees_All_Branches_Even_If_BranchId_Is_Set()
    {
        var dbName = Guid.NewGuid().ToString();
        var (branchA, _) = await SeedTwoBranchCasesAsync(dbName);

        await using var dbAdmin = TestDb.NewContext(dbName,
            new TestCurrentUser(branchA, RoleNames.Admin));
        var visible = dbAdmin.InsuranceCases.ToList();

        Assert.Equal(2, visible.Count);
    }

    [Fact]
    public async Task Null_BranchId_User_Sees_All()
    {
        var dbName = Guid.NewGuid().ToString();
        await SeedTwoBranchCasesAsync(dbName);

        await using var dbNoBranch = TestDb.NewContext(dbName, new TestCurrentUser(branchId: null));
        Assert.Equal(2, dbNoBranch.InsuranceCases.Count());
    }

    [Fact]
    public async Task RetailCases_And_Warehouses_Honor_Same_Scope()
    {
        var dbName = Guid.NewGuid().ToString();
        var (branchA, branchB) = await SeedTwoBranchCasesAsync(dbName);
        await using (var seed = TestDb.NewContext(dbName))
        {
            var customerId = (await seed.Customers.FirstAsync()).Id;
            var vehicleId = (await seed.Vehicles.FirstAsync()).Id;
            seed.RetailCases.Add(new RetailCase
            {
                CaseNumber = "RET-A",
                CustomerId = customerId,
                VehicleId = vehicleId,
                BranchId = branchA,
                Status = RetailCaseStatus.Quoted
            });
            seed.RetailCases.Add(new RetailCase
            {
                CaseNumber = "RET-B",
                CustomerId = customerId,
                VehicleId = vehicleId,
                BranchId = branchB,
                Status = RetailCaseStatus.Quoted
            });
            seed.Warehouses.Add(new Warehouse { BranchId = branchA, Name = "WH-A" });
            seed.Warehouses.Add(new Warehouse { BranchId = branchB, Name = "WH-B" });
            await seed.SaveChangesAsync();
        }

        await using var dbBranchA = TestDb.NewContext(dbName, new TestCurrentUser(branchA));
        Assert.Equal(1, dbBranchA.RetailCases.Count());
        Assert.Equal(1, dbBranchA.Warehouses.Count());
        Assert.Equal("RET-A", dbBranchA.RetailCases.Single().CaseNumber);
        Assert.Equal("WH-A", dbBranchA.Warehouses.Single().Name);
    }

    /// <summary>
    /// Seeds two branches each with one InsuranceCase. Returns (branchA, branchB) ids.
    /// Customer + Vehicle + InsuranceCompany are shared.
    /// </summary>
    private static async Task<(Guid branchA, Guid branchB)> SeedTwoBranchCasesAsync(string dbName)
    {
        await using var db = TestDb.NewContext(dbName);
        var branchA = new Branch { Name = "Branch A", Code = "A", AddressLine = "x", City = "Athens", IsActive = true };
        var branchB = new Branch { Name = "Branch B", Code = "B", AddressLine = "x", City = "Athens", IsActive = true };
        var customer = new Customer
        {
            CustomerType = CustomerType.Individual,
            FirstName = "T",
            LastName = "U",
            MobilePhone = "6900000000",
            GdprConsent = true,
            GdprConsentAt = DateTime.UtcNow,
            IsActive = true
        };
        var vehicle = new Vehicle
        {
            Customer = customer,
            PlateNumber = "AAA-1111",
            Brand = "Toyota",
            Model = "Yaris",
            IsActive = true
        };
        var insurer = new InsuranceCompany { Name = "Ergo", IsActive = true };
        db.Branches.AddRange(branchA, branchB);
        db.Customers.Add(customer);
        db.Vehicles.Add(vehicle);
        db.InsuranceCompanies.Add(insurer);
        db.InsuranceCases.AddRange(
            new InsuranceCase
            {
                CaseNumber = "INS-A",
                Customer = customer,
                Vehicle = vehicle,
                Branch = branchA,
                InsuranceCompany = insurer,
                Status = InsuranceCaseStatus.NewCase
            },
            new InsuranceCase
            {
                CaseNumber = "INS-B",
                Customer = customer,
                Vehicle = vehicle,
                Branch = branchB,
                InsuranceCompany = insurer,
                Status = InsuranceCaseStatus.NewCase
            });
        await db.SaveChangesAsync();
        return (branchA.Id, branchB.Id);
    }
}
