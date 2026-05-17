using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Customers;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class CustomerHandlersTests
{
    private static CustomerUpsertDto IndividualDto(string first = "Γιώργος", string last = "Παπαδόπουλος") =>
        new(CustomerType.Individual, first, last, null, null, null, null,
            MobilePhone: "6912345678", null, null, null, null, null,
            GdprConsent: true, null, IsActive: true);

    private static CustomerUpsertDto CompanyDto(string company = "Acme Α.Ε.", string vat = "099999999") =>
        new(CustomerType.Company, null, null, company, vat, "ΦΑΕ ΠΕΙΡΑΙΑ", null,
            MobilePhone: "2101234567", null, null, null, null, null,
            GdprConsent: true, null, IsActive: true);

    [Fact]
    public async Task Create_Individual_PersistsAndReturnsId()
    {
        await using var db = TestDb.NewContext();
        var handler = new CreateCustomerHandler(db, TimeProvider.System);

        var id = await handler.Handle(new CreateCustomerCommand(IndividualDto()), default);

        Assert.NotEqual(Guid.Empty, id);
        var saved = await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        Assert.NotNull(saved);
        Assert.Equal("Παπαδόπουλος", saved!.LastName);
        Assert.True(saved.GdprConsent);
        Assert.NotNull(saved.GdprConsentAt);
    }

    [Fact]
    public async Task Create_Company_PersistsCorrectly()
    {
        await using var db = TestDb.NewContext();
        var handler = new CreateCustomerHandler(db, TimeProvider.System);

        var id = await handler.Handle(new CreateCustomerCommand(CompanyDto()), default);

        var saved = await db.Customers.FirstAsync(c => c.Id == id);
        Assert.Equal(CustomerType.Company, saved.CustomerType);
        Assert.Equal("Acme Α.Ε.", saved.CompanyName);
        Assert.Equal("099999999", saved.VatNumber);
    }

    [Fact]
    public async Task Validator_Individual_RequiresFirstAndLastName()
    {
        var validator = new CreateCustomerValidator();
        var dto = IndividualDto() with { FirstName = "", LastName = "" };

        var result = await validator.ValidateAsync(new CreateCustomerCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("FirstName"));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("LastName"));
    }

    [Fact]
    public async Task Validator_Company_RequiresCompanyNameAndVat()
    {
        var validator = new CreateCustomerValidator();
        var dto = CompanyDto() with { CompanyName = "", VatNumber = "" };

        var result = await validator.ValidateAsync(new CreateCustomerCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("CompanyName"));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("VatNumber"));
    }

    [Fact]
    public async Task Validator_InvalidPhoneFormat_Fails()
    {
        var validator = new CreateCustomerValidator();
        var dto = IndividualDto() with { MobilePhone = "abc" };

        var result = await validator.ValidateAsync(new CreateCustomerCommand(dto));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("MobilePhone"));
    }

    [Fact]
    public async Task Update_PreservesGdprConsentAt_WhenConsentUnchanged()
    {
        await using var db = TestDb.NewContext();
        var clock = TimeProvider.System;
        var createId = await new CreateCustomerHandler(db, clock)
            .Handle(new CreateCustomerCommand(IndividualDto()), default);
        var original = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == createId);
        var originalConsentAt = original.GdprConsentAt;

        await Task.Delay(50); // ensure clock advances

        var updateDto = IndividualDto() with { FirstName = "Νέο Όνομα" };
        await new UpdateCustomerHandler(db, clock)
            .Handle(new UpdateCustomerCommand(createId, updateDto), default);

        var updated = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == createId);
        Assert.Equal("Νέο Όνομα", updated.FirstName);
        Assert.Equal(originalConsentAt, updated.GdprConsentAt);
    }

    [Fact]
    public async Task Update_ClearsGdprConsentAt_WhenConsentWithdrawn()
    {
        await using var db = TestDb.NewContext();
        var id = await new CreateCustomerHandler(db, TimeProvider.System)
            .Handle(new CreateCustomerCommand(IndividualDto()), default);

        var dto = IndividualDto() with { GdprConsent = false };
        await new UpdateCustomerHandler(db, TimeProvider.System)
            .Handle(new UpdateCustomerCommand(id, dto), default);

        var entity = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == id);
        Assert.False(entity.GdprConsent);
        Assert.Null(entity.GdprConsentAt);
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndHidesFromQueries()
    {
        await using var db = TestDb.NewContext();
        var id = await new CreateCustomerHandler(db, TimeProvider.System)
            .Handle(new CreateCustomerCommand(IndividualDto()), default);

        await new DeleteCustomerHandler(db).Handle(new DeleteCustomerCommand(id), default);

        // Global query filter should hide the soft-deleted row
        var visible = await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        Assert.Null(visible);

        // But the row still exists (use IgnoreQueryFilters to verify)
        var stillThere = await db.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == id);
        Assert.True(stillThere.IsDeleted);
    }

    [Fact]
    public async Task List_FiltersByCustomerType_AndSearch()
    {
        await using var db = TestDb.NewContext();
        var creator = new CreateCustomerHandler(db, TimeProvider.System);
        await creator.Handle(new CreateCustomerCommand(IndividualDto("Γιώργος", "Παπαδόπουλος")), default);
        await creator.Handle(new CreateCustomerCommand(IndividualDto("Άννα", "Καραμανλή")), default);
        await creator.Handle(new CreateCustomerCommand(CompanyDto("Acme Α.Ε.", "099999999")), default);

        var listHandler = new ListCustomersHandler(db);

        var allCompanies = await listHandler.Handle(
            new ListCustomersQuery(CustomerType: CustomerType.Company), default);
        Assert.Single(allCompanies.Items);
        Assert.Equal("Acme Α.Ε.", allCompanies.Items[0].DisplayName);

        var searchKaram = await listHandler.Handle(
            new ListCustomersQuery(Search: "Καραμ"), default);
        Assert.Single(searchKaram.Items);
        Assert.Contains("Καραμανλή", searchKaram.Items[0].DisplayName);

        var all = await listHandler.Handle(new ListCustomersQuery(), default);
        Assert.Equal(3, all.TotalCount);
    }

    [Fact]
    public async Task GetById_ReturnsNull_ForUnknownId()
    {
        await using var db = TestDb.NewContext();
        var handler = new GetCustomerByIdHandler(db);
        var result = await handler.Handle(new GetCustomerByIdQuery(Guid.NewGuid()), default);
        Assert.Null(result);
    }

    [Fact]
    public async Task Update_NotFound_Throws()
    {
        await using var db = TestDb.NewContext();
        var handler = new UpdateCustomerHandler(db, TimeProvider.System);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new UpdateCustomerCommand(Guid.NewGuid(), IndividualDto()), default));
    }
}
