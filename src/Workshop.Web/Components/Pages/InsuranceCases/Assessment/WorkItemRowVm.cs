using Workshop.Application.Features.Assessments;
using Workshop.Domain.Enums;

namespace Workshop.Web.Components.Pages.InsuranceCases.Assessment;

public class WorkItemRowVm
{
    public Guid? Id { get; set; }
    public Guid? BodyPanelId { get; set; }
    public string? PanelCode { get; set; }
    public string Description { get; set; } = string.Empty;

    public decimal? Cost_Polish { get; set; }
    public decimal? Cost_PDR { get; set; }
    public decimal? Cost_RemoveRefit { get; set; }
    public decimal? Cost_Replace { get; set; }
    public decimal? Cost_DisassembleAssemble { get; set; }
    public decimal? Cost_Repair { get; set; }
    public decimal? Cost_Paint { get; set; }
    public decimal? Cost_RepairPaint { get; set; }
    public decimal? Cost_Weld { get; set; }
    public decimal? Cost_Other { get; set; }

    public decimal? DiscountPct { get; set; }

    /// <summary>Allowed operations from the BodyPanel × Operation matrix.</summary>
    public HashSet<OperationType>? AllowedOperations { get; set; }

    public decimal? Cost(OperationType op) => op switch
    {
        OperationType.Polish => Cost_Polish,
        OperationType.PDR => Cost_PDR,
        OperationType.RemoveRefit => Cost_RemoveRefit,
        OperationType.Replace => Cost_Replace,
        OperationType.DisassembleAssemble => Cost_DisassembleAssemble,
        OperationType.Repair => Cost_Repair,
        OperationType.Paint => Cost_Paint,
        OperationType.RepairPaint => Cost_RepairPaint,
        OperationType.Weld => Cost_Weld,
        OperationType.Other => Cost_Other,
        _ => null
    };

    public void SetCost(OperationType op, decimal? value)
    {
        switch (op)
        {
            case OperationType.Polish: Cost_Polish = value; break;
            case OperationType.PDR: Cost_PDR = value; break;
            case OperationType.RemoveRefit: Cost_RemoveRefit = value; break;
            case OperationType.Replace: Cost_Replace = value; break;
            case OperationType.DisassembleAssemble: Cost_DisassembleAssemble = value; break;
            case OperationType.Repair: Cost_Repair = value; break;
            case OperationType.Paint: Cost_Paint = value; break;
            case OperationType.RepairPaint: Cost_RepairPaint = value; break;
            case OperationType.Weld: Cost_Weld = value; break;
            case OperationType.Other: Cost_Other = value; break;
        }
    }

    public decimal Total
    {
        get
        {
            var dto = ToUpsertDto();
            return WorkItemCalculator.RowTotal(dto);
        }
    }

    public WorkItemUpsertDto ToUpsertDto() => new(
        Id, BodyPanelId, Description,
        Cost_Polish, Cost_PDR, Cost_RemoveRefit, Cost_Replace, Cost_DisassembleAssemble,
        Cost_Repair, Cost_Paint, Cost_RepairPaint, Cost_Weld, Cost_Other, DiscountPct);
}
