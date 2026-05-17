using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Common;

namespace Workshop.Infrastructure.Persistence;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser, TimeProvider clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null) return;

        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is Entity entity)
            {
                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = now;
                    entity.UpdatedAt = now;
                    entity.CreatedById ??= userId;
                    entity.UpdatedById ??= userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entity.UpdatedAt = now;
                    entity.UpdatedById = userId;
                }
                else if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable soft)
                {
                    entry.State = EntityState.Modified;
                    soft.IsDeleted = true;
                    entity.UpdatedAt = now;
                    entity.UpdatedById = userId;
                }
            }
        }
    }
}

public class NullCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;
    public Guid? BranchId => null;
    public Guid? CustomerId => null;
    public Guid? InsuranceCompanyId => null;
    public Guid? SupplierId => null;
    public Workshop.Domain.Enums.PortalAudience? Audience => null;
    public bool IsAuthenticated => false;
    public bool IsInRole(string role) => false;
    public bool IsAnyOf(params string[] roles) => false;
}

// Default for migrations / tests / background jobs that have no UI: no override.
public class NullBranchScopeState : IBranchScopeState
{
    public Guid? OverrideBranchId => null;
    public void SetOverride(Guid? branchId) { /* no-op */ }
}
