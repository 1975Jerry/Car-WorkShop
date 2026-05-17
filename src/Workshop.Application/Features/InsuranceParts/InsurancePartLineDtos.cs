using Workshop.Domain.Enums;

namespace Workshop.Application.Features.InsuranceParts;

public record InsurancePartLineDto(
    Guid Id,
    Guid AssessmentId,
    Guid? SupplierId,
    string? SupplierName,
    Guid DestinationBranchId,
    string DestinationBranchName,
    PartType PartType,
    string PartName,
    decimal Quantity,
    decimal UnitCost,
    decimal? DiscountPct,
    decimal Total,
    AvailabilityStatus AvailabilityStatus,
    bool InsuranceApproved,
    bool Ordered,
    DateOnly? OrderDate,
    PartReceivedStatus ReceivedStatus,
    DateOnly? ReceivedDate,
    Guid? WarehouseId,
    string? WarehouseName,
    string? StorageLocation,
    string? Notes,
    DateTime UpdatedAt);

public record InsurancePartLineUpsertDto(
    Guid? SupplierId,
    Guid DestinationBranchId,
    PartType PartType,
    string PartName,
    decimal Quantity,
    decimal UnitCost,
    decimal? DiscountPct,
    AvailabilityStatus AvailabilityStatus,
    bool InsuranceApproved,
    string? Notes);
