using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Common.Notifications;

public interface ICaseNotificationRecipients
{
    Task<IReadOnlyList<NotificationRecipient>> ResolveAsync(
        Guid? insuranceCaseId,
        Guid? retailCaseId,
        CaseAudienceFlags audiences,
        CancellationToken ct = default);
}

[Flags]
public enum CaseAudienceFlags
{
    None = 0,
    Customer = 1,
    AssignedStaff = 2,
    InsuranceReviewers = 4,
    Suppliers = 8,
    All = Customer | AssignedStaff | InsuranceReviewers | Suppliers,
}

public class CaseNotificationRecipients(IWorkshopDbContext db) : ICaseNotificationRecipients
{
    public async Task<IReadOnlyList<NotificationRecipient>> ResolveAsync(
        Guid? insuranceCaseId,
        Guid? retailCaseId,
        CaseAudienceFlags audiences,
        CancellationToken ct = default)
    {
        if (audiences == CaseAudienceFlags.None || (insuranceCaseId is null && retailCaseId is null))
            return [];

        var (customerId, branchId, insuranceCompanyId, assignedUserId, supplierIds) =
            await LoadCaseContextAsync(insuranceCaseId, retailCaseId, ct);

        var users = db.Users.AsNoTracking().Where(u => u.IsActive);
        var predicates = new List<System.Linq.Expressions.Expression<Func<Domain.Entities.Identity.User, bool>>>();

        if (audiences.HasFlag(CaseAudienceFlags.Customer) && customerId is { } cid)
            predicates.Add(u => u.PortalAudience == PortalAudience.Customer && u.CustomerId == cid);

        if (audiences.HasFlag(CaseAudienceFlags.AssignedStaff) && assignedUserId is { } aid)
            predicates.Add(u => u.Id == aid);

        if (audiences.HasFlag(CaseAudienceFlags.InsuranceReviewers) && insuranceCompanyId is { } icid)
            predicates.Add(u => u.PortalAudience == PortalAudience.Insurance && u.InsuranceCompanyId == icid);

        if (audiences.HasFlag(CaseAudienceFlags.Suppliers) && supplierIds.Count > 0)
            predicates.Add(u => u.PortalAudience == PortalAudience.Supplier && u.SupplierId != null && supplierIds.Contains(u.SupplierId.Value));

        if (predicates.Count == 0)
            return [];

        var query = users.Where(predicates[0]);
        for (var i = 1; i < predicates.Count; i++)
            query = query.Union(db.Users.AsNoTracking().Where(u => u.IsActive).Where(predicates[i]));

        var rows = await query
            .Select(u => new { u.Id, u.Email, u.PhoneNumber, u.Language })
            .ToListAsync(ct);

        return rows
            .Select(r => new NotificationRecipient(
                r.Id,
                r.Email,
                r.PhoneNumber,
                string.IsNullOrEmpty(r.Language) ? "el" : r.Language,
                NotificationChannels.All))
            .ToList();
    }

    private async Task<(Guid? CustomerId, Guid? BranchId, Guid? InsuranceCompanyId, Guid? AssignedUserId, IReadOnlyList<Guid> SupplierIds)>
        LoadCaseContextAsync(Guid? insuranceCaseId, Guid? retailCaseId, CancellationToken ct)
    {
        if (insuranceCaseId is { } iid)
        {
            var ctx = await db.InsuranceCases.AsNoTracking()
                .Where(c => c.Id == iid)
                .Select(c => new
                {
                    CustomerId = (Guid?)c.CustomerId,
                    BranchId = (Guid?)c.BranchId,
                    InsuranceCompanyId = (Guid?)c.InsuranceCompanyId,
                    c.AssignedUserId,
                })
                .FirstOrDefaultAsync(ct);
            if (ctx is null)
                return (null, null, null, null, []);

            var suppliers = await db.InsurancePartLines.AsNoTracking()
                .Where(p => p.Assessment.InsuranceCaseId == iid && p.SupplierId != null)
                .Select(p => p.SupplierId!.Value)
                .Distinct()
                .ToListAsync(ct);

            return (ctx.CustomerId, ctx.BranchId, ctx.InsuranceCompanyId, ctx.AssignedUserId, suppliers);
        }

        if (retailCaseId is { } rid)
        {
            var ctx = await db.RetailCases.AsNoTracking()
                .Where(c => c.Id == rid)
                .Select(c => new
                {
                    CustomerId = (Guid?)c.CustomerId,
                    BranchId = (Guid?)c.BranchId,
                    c.AssignedUserId,
                })
                .FirstOrDefaultAsync(ct);
            if (ctx is null)
                return (null, null, null, null, []);

            var suppliers = await db.RetailPartLines.AsNoTracking()
                .Where(p => p.RetailCaseId == rid && p.SupplierId != null)
                .Select(p => p.SupplierId!.Value)
                .Distinct()
                .ToListAsync(ct);

            return (ctx.CustomerId, ctx.BranchId, null, ctx.AssignedUserId, suppliers);
        }

        return (null, null, null, null, []);
    }
}
