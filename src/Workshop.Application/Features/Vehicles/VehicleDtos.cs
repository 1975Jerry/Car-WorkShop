using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Vehicles;

public record VehicleListItemDto(
    Guid Id,
    string PlateNumber,
    string Brand,
    string Model,
    int? Year,
    string? Color,
    string CustomerDisplayName,
    Guid CustomerId,
    string? InsuranceCompanyName,
    bool IsActive,
    DateTime CreatedAt);

public record VehicleDetailDto(
    Guid Id,
    Guid CustomerId,
    string PlateNumber,
    string? Vin,
    string Brand,
    string Model,
    string? Version,
    int? Year,
    string? Color,
    FuelType? FuelType,
    int? Mileage,
    Guid? InsuranceCompanyId,
    string? PolicyNumber,
    DateOnly? InsuranceExpiration,
    string? Notes,
    bool IsActive);

public record VehicleUpsertDto(
    Guid CustomerId,
    string PlateNumber,
    string? Vin,
    string Brand,
    string Model,
    string? Version,
    int? Year,
    string? Color,
    FuelType? FuelType,
    int? Mileage,
    Guid? InsuranceCompanyId,
    string? PolicyNumber,
    DateOnly? InsuranceExpiration,
    string? Notes,
    bool IsActive);
