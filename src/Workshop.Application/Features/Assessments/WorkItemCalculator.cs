using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Assessments;

public static class WorkItemCalculator
{
    public static IReadOnlyDictionary<OperationType, decimal?> CostsByOperation(WorkItemUpsertDto w) =>
        new Dictionary<OperationType, decimal?>
        {
            [OperationType.Polish] = w.Cost_Polish,
            [OperationType.PDR] = w.Cost_PDR,
            [OperationType.RemoveRefit] = w.Cost_RemoveRefit,
            [OperationType.Replace] = w.Cost_Replace,
            [OperationType.DisassembleAssemble] = w.Cost_DisassembleAssemble,
            [OperationType.Repair] = w.Cost_Repair,
            [OperationType.Paint] = w.Cost_Paint,
            [OperationType.RepairPaint] = w.Cost_RepairPaint,
            [OperationType.Weld] = w.Cost_Weld,
            [OperationType.Other] = w.Cost_Other
        };

    public static decimal SumCosts(WorkItemUpsertDto w) =>
        (w.Cost_Polish ?? 0)
        + (w.Cost_PDR ?? 0)
        + (w.Cost_RemoveRefit ?? 0)
        + (w.Cost_Replace ?? 0)
        + (w.Cost_DisassembleAssemble ?? 0)
        + (w.Cost_Repair ?? 0)
        + (w.Cost_Paint ?? 0)
        + (w.Cost_RepairPaint ?? 0)
        + (w.Cost_Weld ?? 0)
        + (w.Cost_Other ?? 0);

    public static decimal RowTotal(WorkItemUpsertDto w)
    {
        var raw = SumCosts(w);
        var discount = w.DiscountPct ?? 0m;
        if (discount < 0) discount = 0;
        if (discount > 100) discount = 100;
        return Math.Round(raw * (1 - discount / 100m), 2, MidpointRounding.AwayFromZero);
    }
}
