using Workshop.Domain.Enums;

namespace Workshop.Application.Features.RetailParts;

public record RetailPartLineDto(
    Guid Id,
    Guid RetailCaseId,
    Guid? SupplierId,
    string? SupplierName,
    Guid DestinationBranchId,
    string DestinationBranchName,
    PartType PartType,
    string PartName,
    decimal Quantity,
    decimal UnitCost,
    decimal Total,
    PartReceivedStatus ReceivedStatus,
    Guid? WarehouseId,
    string? WarehouseName,
    string? StorageLocation,
    string? Notes,
    DateTime UpdatedAt);

public record RetailPartLineUpsertDto(
    Guid? SupplierId,
    Guid DestinationBranchId,
    PartType PartType,
    string PartName,
    decimal Quantity,
    decimal UnitCost,
    string? Notes);
