using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;
using Workshop.Domain.Workflows;

namespace Workshop.Application.Features.InsuranceCases;

public interface ICaseGuardContextBuilder
{
    Task<CaseGuardContext> BuildAsync(Guid caseId, CancellationToken ct = default);
}

public class CaseGuardContextBuilder(IWorkshopDbContext db, ICurrentUserService user)
    : ICaseGuardContextBuilder
{
    public async Task<CaseGuardContext> BuildAsync(Guid caseId, CancellationToken ct = default)
    {
        var c = await db.InsuranceCases.AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new
            {
                x.AssessorId,
                x.AccidentDate,
                AssessmentExists = x.Assessment != null,
                AssessmentLaborCost = x.Assessment != null ? x.Assessment.LaborCost : 0,
                AssessmentTotal = x.Assessment != null ? x.Assessment.TotalEstimatedCost : 0,
                AssessmentAgreed = x.Assessment != null ? x.Assessment.AgreedAmount : 0,
                AssessmentDate = x.Assessment != null ? (DateOnly?)x.Assessment.AssessmentDate : null,
                IntermediateInspection = x.Assessment != null && x.Assessment.IntermediateInspection,
                IntermediateInspectionDone = x.Repair != null && x.Repair.IntermediateInspectionDone,
                WorkItemCount = x.Assessment == null ? 0 : x.Assessment.WorkItems.Count(),
                PartLineCount = x.Assessment == null ? 0 : x.Assessment.PartLines.Count(),
                AllPartsReceived = x.Assessment == null
                    || x.Assessment.PartLines.All(p =>
                        p.ReceivedStatus == PartReceivedStatus.Received
                        || p.ReceivedStatus == PartReceivedStatus.Cancelled),
                HasCurrentQuote = x.Quotes.Any(q => q.IsCurrent && !string.IsNullOrEmpty(q.PdfPath)),
                HasApproval = x.Approval != null,
                ApprovalStatus = x.Approval != null ? (ApprovalStatus?)x.Approval.ApprovalStatus : null,
                CustomerParticipation = x.Approval != null && x.Approval.CustomerParticipation,
                ParticipationAmount = x.Approval != null ? x.Approval.ParticipationAmount : null,
                HasRepairSchedule = x.Repair != null && x.Repair.TechnicianId != null,
                RepairCompleted = x.Repair != null && x.Repair.CompletionDate != null,
                AgreedAmount = x.Approval != null ? x.Approval.ApprovedAmount : 0,
                IntakePhotoCount = x.Assessment == null ? 0
                    : x.Assessment.Photos.Count(p => p.Phase == PhotoPhase.Intake || p.Phase == PhotoPhase.Damage),
                CompletionPhotoCount = x.Repair == null ? 0
                    : x.Repair.Photos.Count(p => p.Phase == PhotoPhase.Completion),
                DocumentTypes = x.Documents.Select(d => d.DocumentType).ToArray(),
                SentToInsuranceCount = x.Documents.Count(d => d.SentToInsurance),
                TotalPaymentsAmount = x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Case {caseId} not found");

        var requiredDocsForApproval = new[] { DocumentType.CaseForm, DocumentType.InsuranceForm };
        var hasRequiredDocs = requiredDocsForApproval.All(t => c.DocumentTypes.Contains(t));

        var participationConfirmed = !c.CustomerParticipation || (c.ParticipationAmount ?? 0) >= 0;
        // ^ For now: customer-participation acceptance = approval has the value recorded.
        // A separate "customer accepted" flag should be added later (open item §8 in DOMAIN-MODEL).

        return new CaseGuardContext
        {
            HasAssessorAssigned = c.AssessorId != null,
            HasAccidentDate = c.AccidentDate != null,
            IntakePhotoCount = c.IntakePhotoCount,
            HasAssessmentCompleted = c.AssessmentExists
                                     && c.AssessmentDate.HasValue
                                     && c.AssessmentTotal > 0
                                     && c.AssessmentAgreed > 0,
            WorkItemCount = c.WorkItemCount,
            HasCurrentQuote = c.HasCurrentQuote,
            HasRequiredDocumentsForApprovalSubmission = hasRequiredDocs,
            HasApproval = c.HasApproval,
            ApprovalIsPositive = c.ApprovalStatus == ApprovalStatus.Approved
                                 || c.ApprovalStatus == ApprovalStatus.PartialApproval,
            ApprovalIsRejected = c.ApprovalStatus == ApprovalStatus.Rejected,
            CustomerParticipationConfirmed = participationConfirmed,
            AllPartsReceivedOrNotNeeded = c.AllPartsReceived,
            RepairScheduledWithTechnician = c.HasRepairSchedule,
            RepairCompletionDateSet = c.RepairCompleted,
            CompletionPhotoCount = c.CompletionPhotoCount,
            IntermediateInspectionDoneIfRequired = !c.IntermediateInspection || c.IntermediateInspectionDone,
            SettlementIssued = c.AgreedAmount > 0,
            PaymentsCoverAgreedAmount = c.TotalPaymentsAmount >= c.AgreedAmount,
            RequiredDocumentsSentToInsurance = c.SentToInsuranceCount > 0,
            ActorIsAdminOrBranchManager = user.IsAnyOf("Admin", "BranchManager")
        };
    }
}
