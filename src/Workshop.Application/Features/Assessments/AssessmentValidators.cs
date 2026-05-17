using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Assessments;

public class AssessmentUpsertValidator : AbstractValidator<AssessmentUpsertDto>
{
    public AssessmentUpsertValidator()
    {
        RuleFor(x => x.AssessmentDate).NotEmpty();
        RuleFor(x => x.AgreementDate).NotEmpty();
        RuleFor(x => x.AgreedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PartsCost)
            .NotNull().GreaterThanOrEqualTo(0)
            .When(x => x.PartsRequired);

        RuleForEach(x => x.WorkItems).SetValidator(new WorkItemUpsertValidator());

        RuleFor(x => x.WorkItems)
            .NotEmpty().WithMessage("At least one work item is required.");
    }
}

public class WorkItemUpsertValidator : AbstractValidator<WorkItemUpsertDto>
{
    public WorkItemUpsertValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.DiscountPct).InclusiveBetween(0, 100).When(x => x.DiscountPct.HasValue);

        RuleFor(x => x).Must(HaveAtLeastOneCost)
            .WithMessage("Work item must have at least one non-zero cost.");

        // Non-negative individual costs
        Foreach(b => b.Cost_Polish);
        Foreach(b => b.Cost_PDR);
        Foreach(b => b.Cost_RemoveRefit);
        Foreach(b => b.Cost_Replace);
        Foreach(b => b.Cost_DisassembleAssemble);
        Foreach(b => b.Cost_Repair);
        Foreach(b => b.Cost_Paint);
        Foreach(b => b.Cost_RepairPaint);
        Foreach(b => b.Cost_Weld);
        Foreach(b => b.Cost_Other);
    }

    private void Foreach(System.Linq.Expressions.Expression<Func<WorkItemUpsertDto, decimal?>> selector) =>
        RuleFor(selector).GreaterThanOrEqualTo(0).When(x => selector.Compile()(x).HasValue);

    private static bool HaveAtLeastOneCost(WorkItemUpsertDto w) =>
        WorkItemCalculator.SumCosts(w) > 0;
}

/// <summary>
/// Cross-row validator that needs the DB to enforce the panel × operation matrix from
/// ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ2.xlsx — a WorkItem may only carry a non-zero cost on operations
/// whose row exists in BodyPanelOperation for its BodyPanel.
/// </summary>
public interface IAllowedOpsValidator
{
    Task<IReadOnlyList<string>> ValidateAsync(IReadOnlyList<WorkItemUpsertDto> items, CancellationToken ct);
}

public class AllowedOpsValidator(IWorkshopDbContext db) : IAllowedOpsValidator
{
    public async Task<IReadOnlyList<string>> ValidateAsync(
        IReadOnlyList<WorkItemUpsertDto> items, CancellationToken ct)
    {
        var errors = new List<string>();
        var panelIds = items
            .Where(i => i.BodyPanelId.HasValue)
            .Select(i => i.BodyPanelId!.Value)
            .Distinct()
            .ToArray();
        if (panelIds.Length == 0) return errors;

        var allowed = await db.BodyPanelOperations.AsNoTracking()
            .Where(o => panelIds.Contains(o.BodyPanelId))
            .Select(o => new { o.BodyPanelId, o.Operation })
            .ToListAsync(ct);
        var allowedMap = allowed
            .GroupBy(a => a.BodyPanelId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Operation).ToHashSet());

        var codes = await db.BodyPanels.AsNoTracking()
            .Where(p => panelIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Code, ct);

        for (var i = 0; i < items.Count; i++)
        {
            var w = items[i];
            if (w.BodyPanelId is null) continue;
            if (!allowedMap.TryGetValue(w.BodyPanelId.Value, out var ops)) continue;

            var costs = WorkItemCalculator.CostsByOperation(w);
            foreach (var (op, cost) in costs)
            {
                if (cost.GetValueOrDefault() > 0 && !ops.Contains(op))
                {
                    var code = codes.TryGetValue(w.BodyPanelId.Value, out var c) ? c : "?";
                    errors.Add($"WorkItems[{i}]: Operation '{op}' is not allowed for panel '{code}'.");
                }
            }
        }
        return errors;
    }
}
