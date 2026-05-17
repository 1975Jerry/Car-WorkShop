using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Workshop.Domain.Common;

namespace Workshop.Application.Tests;

/// <summary>
/// Pure model-introspection checks. Catches mapping drift between domain code
/// and the EF model without spinning up Postgres.
/// </summary>
public class ModelConsistencyTests
{
    private static IModel Model
    {
        get
        {
            using var db = TestDb.NewContext();
            return db.Model;
        }
    }

    [Fact]
    public void EveryEntityHasGuidPrimaryKey()
    {
        var failures = new List<string>();
        foreach (var et in Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(et.ClrType)) continue;
            var pk = et.FindPrimaryKey();
            if (pk is null) { failures.Add($"{et.ClrType.Name}: no primary key"); continue; }
            if (pk.Properties.Count != 1 || pk.Properties[0].ClrType != typeof(Guid))
                failures.Add($"{et.ClrType.Name}: PK is not a single Guid");
        }
        Assert.Empty(failures);
    }

    [Fact]
    public void EverySoftDeletableEntityHasGlobalQueryFilter()
    {
        var failures = new List<string>();
        foreach (var et in Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(et.ClrType)) continue;
            // Identity-managed types (User, Role) are tracked by ASP.NET Identity and
            // don't get the soft-delete filter — exclude them.
            if (et.ClrType.Namespace?.Contains("Identity") == true) continue;
            if (et.GetQueryFilter() is null)
                failures.Add($"{et.ClrType.Name}: missing IsDeleted query filter");
        }
        Assert.Empty(failures);
    }

    [Fact]
    public void EveryDecimalPropertyHasExplicitPrecision()
    {
        // EF Core warns at runtime if decimal precision isn't set. Make it a hard fail
        // at model-build time so the warning never reaches Postgres.
        var failures = new List<string>();
        foreach (var et in Model.GetEntityTypes())
        {
            foreach (var p in et.GetProperties())
            {
                var clr = p.ClrType;
                var underlying = Nullable.GetUnderlyingType(clr) ?? clr;
                if (underlying != typeof(decimal)) continue;

                var precision = p.GetPrecision();
                if (precision is null || precision <= 0)
                    failures.Add($"{et.ClrType.Name}.{p.Name}: decimal without HasPrecision");
            }
        }
        Assert.Empty(failures);
    }

    [Theory]
    [InlineData("Branch", "Code")]
    [InlineData("Vehicle", "PlateNumber")]
    [InlineData("InsuranceCompany", "Name")]
    [InlineData("BodyPanel", "Code")]
    [InlineData("InsuranceCase", "CaseNumber")]
    [InlineData("RetailCase", "CaseNumber")]
    [InlineData("ServiceCatalog", "Code")]
    public void ExpectedUniqueIndexExists(string entityName, string propertyName)
    {
        var et = Model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == entityName);
        Assert.NotNull(et);
        var idx = et!.GetIndexes().FirstOrDefault(i =>
            i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == propertyName);
        Assert.NotNull(idx);
    }

    [Fact]
    public void InsuranceCase_HasOneToOneNavigationsConfigured()
    {
        var et = Model.GetEntityTypes().First(e => e.ClrType.Name == "InsuranceCase");
        var navigations = et.GetNavigations().Select(n => n.Name).ToHashSet();
        Assert.Contains("Assessment", navigations);
        Assert.Contains("Approval", navigations);
        Assert.Contains("Repair", navigations);
    }

    [Fact]
    public void RetailCase_OneToOneRepairConfigured()
    {
        var et = Model.GetEntityTypes().First(e => e.ClrType.Name == "RetailCase");
        var navigations = et.GetNavigations().Select(n => n.Name).ToHashSet();
        Assert.Contains("Repair", navigations);
    }

    [Fact]
    public void Photo_AllowsBothInsuranceAndRetailParents()
    {
        // The Photo schema must support FK to either Assessment, Repair, or RetailRepair —
        // all three nullable so a photo can attach to exactly one parent.
        var et = Model.GetEntityTypes().First(e => e.ClrType.Name == "Photo");
        var fkProps = et.GetProperties().Where(p =>
            p.Name is "AssessmentId" or "RepairId" or "RetailRepairId").ToList();
        Assert.Equal(3, fkProps.Count);
        Assert.All(fkProps, p => Assert.True(p.IsNullable, $"{p.Name} must be nullable"));
    }

    [Fact]
    public void DocumentAndPayment_FkToInsuranceOrRetailIsNullable()
    {
        foreach (var name in new[] { "Document", "Payment" })
        {
            var et = Model.GetEntityTypes().First(e => e.ClrType.Name == name);
            var ic = et.GetProperties().First(p => p.Name == "InsuranceCaseId");
            var rc = et.GetProperties().First(p => p.Name == "RetailCaseId");
            Assert.True(ic.IsNullable, $"{name}.InsuranceCaseId must be nullable");
            Assert.True(rc.IsNullable, $"{name}.RetailCaseId must be nullable");
        }
    }

    [Fact]
    public void User_PortalAndScopingForeignKeysPresent()
    {
        var et = Model.GetEntityTypes().First(e => e.ClrType.Name == "User");
        var props = et.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("PortalAudience", props);
        Assert.Contains("CustomerId", props);
        Assert.Contains("InsuranceCompanyId", props);
        Assert.Contains("SupplierId", props);
        Assert.Contains("BranchId", props);
    }
}
