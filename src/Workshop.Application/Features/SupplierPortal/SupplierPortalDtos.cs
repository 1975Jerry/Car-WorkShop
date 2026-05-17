using Workshop.Domain.Enums;

namespace Workshop.Application.Features.SupplierPortal;

public enum SupplierLineKind { Insurance, Retail }

public record SupplierOrderRow(
    Guid LineId,
    SupplierLineKind Kind,
    string CaseNumber,
    string PartName,
    PartType PartType,
    decimal Quantity,
    decimal UnitCost,
    decimal Total,
    PartReceivedStatus ReceivedStatus,
    string DestinationBranchName,
    DateOnly? OrderDate,
    string? Notes,
    DateTime UpdatedAt);
