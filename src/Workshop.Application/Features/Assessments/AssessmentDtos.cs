namespace Workshop.Application.Features.Assessments;

public record WorkItemDto(
    Guid? Id,
    Guid? BodyPanelId,
    string? BodyPanelCode,
    string Description,
    decimal? Cost_Polish,
    decimal? Cost_PDR,
    decimal? Cost_RemoveRefit,
    decimal? Cost_Replace,
    decimal? Cost_DisassembleAssemble,
    decimal? Cost_Repair,
    decimal? Cost_Paint,
    decimal? Cost_RepairPaint,
    decimal? Cost_Weld,
    decimal? Cost_Other,
    decimal? DiscountPct,
    decimal Total);

public record AssessmentReadDto(
    Guid? Id,
    Guid InsuranceCaseId,
    DateOnly AssessmentDate,
    decimal LaborCost,
    bool PartsRequired,
    decimal? PartsCost,
    decimal? PaintMaterialsCost,
    decimal TotalEstimatedCost,
    decimal AgreedAmount,
    DateOnly AgreementDate,
    bool IntermediateInspection,
    string? Notes,
    IReadOnlyList<WorkItemDto> WorkItems);

public record AssessmentUpsertDto(
    DateOnly AssessmentDate,
    bool PartsRequired,
    decimal? PartsCost,
    decimal? PaintMaterialsCost,
    decimal AgreedAmount,
    DateOnly AgreementDate,
    bool IntermediateInspection,
    string? Notes,
    IReadOnlyList<WorkItemUpsertDto> WorkItems);

public record WorkItemUpsertDto(
    Guid? Id,
    Guid? BodyPanelId,
    string Description,
    decimal? Cost_Polish,
    decimal? Cost_PDR,
    decimal? Cost_RemoveRefit,
    decimal? Cost_Replace,
    decimal? Cost_DisassembleAssemble,
    decimal? Cost_Repair,
    decimal? Cost_Paint,
    decimal? Cost_RepairPaint,
    decimal? Cost_Weld,
    decimal? Cost_Other,
    decimal? DiscountPct);
