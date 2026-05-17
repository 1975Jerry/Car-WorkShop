namespace Workshop.Application.Common.Abstractions;

// Optional per-circuit override that lets an admin narrow their view to a single
// branch via the app-bar selector. Non-admins ignore this — their scope is locked
// to their assigned BranchId by WorkshopDbContext.CurrentScopeBranchId.
public interface IBranchScopeState
{
    Guid? OverrideBranchId { get; }
    void SetOverride(Guid? branchId);
}
