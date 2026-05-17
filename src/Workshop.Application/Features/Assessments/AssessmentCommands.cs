using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Assessments;

public record UpsertAssessmentCommand(Guid InsuranceCaseId, AssessmentUpsertDto Data) : IRequest<Guid>;

public class UpsertAssessmentHandler(IWorkshopDbContext db, IAllowedOpsValidator allowedOps)
    : IRequestHandler<UpsertAssessmentCommand, Guid>
{
    public async Task<Guid> Handle(UpsertAssessmentCommand cmd, CancellationToken ct)
    {
        var caseExists = await db.InsuranceCases.AsNoTracking()
            .AnyAsync(c => c.Id == cmd.InsuranceCaseId, ct);
        if (!caseExists)
            throw new KeyNotFoundException($"Insurance case {cmd.InsuranceCaseId} not found");

        var allowedErrors = await allowedOps.ValidateAsync(cmd.Data.WorkItems, ct);
        if (allowedErrors.Count > 0)
            throw new ValidationException(string.Join(" | ", allowedErrors));

        var assessment = await db.Assessments
            .Include(a => a.WorkItems)
            .FirstOrDefaultAsync(a => a.InsuranceCaseId == cmd.InsuranceCaseId, ct);

        if (assessment is null)
        {
            assessment = new Assessment { InsuranceCaseId = cmd.InsuranceCaseId };
            db.Assessments.Add(assessment);
        }

        ApplyHeader(assessment, cmd.Data);
        SyncWorkItems(db, assessment, cmd.Data.WorkItems);
        RecomputeTotals(assessment);

        await db.SaveChangesAsync(ct);
        return assessment.Id;
    }

    private static void ApplyHeader(Assessment a, AssessmentUpsertDto d)
    {
        a.AssessmentDate = d.AssessmentDate;
        a.PartsRequired = d.PartsRequired;
        a.PartsCost = d.PartsRequired ? d.PartsCost : null;
        a.PaintMaterialsCost = d.PaintMaterialsCost;
        a.AgreedAmount = d.AgreedAmount;
        a.AgreementDate = d.AgreementDate;
        a.IntermediateInspection = d.IntermediateInspection;
        a.Notes = d.Notes;
    }

    private static void SyncWorkItems(IWorkshopDbContext db, Assessment a, IReadOnlyList<WorkItemUpsertDto> incoming)
    {
        var existingById = a.WorkItems.ToDictionary(w => w.Id);
        var existingIds = existingById.Keys.ToHashSet();
        var keepIds = new HashSet<Guid>();

        foreach (var dto in incoming)
        {
            WorkItem entity;
            if (dto.Id is Guid id && existingById.TryGetValue(id, out var existing))
            {
                entity = existing;
                keepIds.Add(id);
            }
            else
            {
                entity = new WorkItem { AssessmentId = a.Id, Assessment = a };
                a.WorkItems.Add(entity);
                db.WorkItems.Add(entity);
            }

            entity.BodyPanelId = dto.BodyPanelId;
            entity.Description = dto.Description;
            entity.Cost_Polish = dto.Cost_Polish;
            entity.Cost_PDR = dto.Cost_PDR;
            entity.Cost_RemoveRefit = dto.Cost_RemoveRefit;
            entity.Cost_Replace = dto.Cost_Replace;
            entity.Cost_DisassembleAssemble = dto.Cost_DisassembleAssemble;
            entity.Cost_Repair = dto.Cost_Repair;
            entity.Cost_Paint = dto.Cost_Paint;
            entity.Cost_RepairPaint = dto.Cost_RepairPaint;
            entity.Cost_Weld = dto.Cost_Weld;
            entity.Cost_Other = dto.Cost_Other;
            entity.DiscountPct = dto.DiscountPct;
            entity.Total = WorkItemCalculator.RowTotal(dto);
        }

        var removed = a.WorkItems
            .Where(w => existingIds.Contains(w.Id) && !keepIds.Contains(w.Id))
            .ToList();
        foreach (var w in removed)
        {
            a.WorkItems.Remove(w);
            db.WorkItems.Remove(w);
        }
    }

    private static void RecomputeTotals(Assessment a)
    {
        a.LaborCost = Math.Round(a.WorkItems.Sum(w => w.Total), 2, MidpointRounding.AwayFromZero);
        a.TotalEstimatedCost = Math.Round(
            a.LaborCost + (a.PartsCost ?? 0) + (a.PaintMaterialsCost ?? 0),
            2, MidpointRounding.AwayFromZero);
    }
}

public class UpsertAssessmentValidator : AbstractValidator<UpsertAssessmentCommand>
{
    public UpsertAssessmentValidator()
    {
        RuleFor(x => x.InsuranceCaseId).NotEmpty();
        RuleFor(x => x.Data).SetValidator(new AssessmentUpsertValidator());
    }
}
