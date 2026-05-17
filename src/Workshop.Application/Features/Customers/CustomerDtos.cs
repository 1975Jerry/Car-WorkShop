using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Customers;

public record CustomerListItemDto(
    Guid Id,
    CustomerType CustomerType,
    string DisplayName,
    string? VatNumber,
    string MobilePhone,
    string? Email,
    string? City,
    int VehicleCount,
    bool IsActive,
    DateTime CreatedAt);

public record CustomerDetailDto(
    Guid Id,
    CustomerType CustomerType,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? VatNumber,
    string? TaxOffice,
    string? IdNumber,
    string MobilePhone,
    string? SecondaryPhone,
    string? Email,
    string? AddressLine,
    string? City,
    string? PostalCode,
    bool GdprConsent,
    DateTime? GdprConsentAt,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CustomerUpsertDto(
    CustomerType CustomerType,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? VatNumber,
    string? TaxOffice,
    string? IdNumber,
    string MobilePhone,
    string? SecondaryPhone,
    string? Email,
    string? AddressLine,
    string? City,
    string? PostalCode,
    bool GdprConsent,
    string? Notes,
    bool IsActive);
