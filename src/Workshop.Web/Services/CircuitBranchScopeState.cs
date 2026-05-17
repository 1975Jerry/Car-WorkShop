using Workshop.Application.Common.Abstractions;

namespace Workshop.Web.Services;

// Lives for the lifetime of a Blazor Server circuit (registered Scoped).
// Held by the app-bar selector in MainLayout; consulted by WorkshopDbContext
// every time a query filter evaluates. Mutating it does NOT auto-refresh
// open pages — MainLayout force-navigates to the current URL on change.
public class CircuitBranchScopeState : IBranchScopeState
{
    public Guid? OverrideBranchId { get; private set; }

    public void SetOverride(Guid? branchId) => OverrideBranchId = branchId;
}
