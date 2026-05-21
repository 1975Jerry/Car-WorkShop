using System.Linq.Expressions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Common;
using Workshop.Domain.Entities.CrossCutting;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Entities.Shared;

namespace Workshop.Infrastructure.Persistence;

public class WorkshopDbContext : IdentityDbContext<User, Role, Guid>, IWorkshopDbContext, IDataProtectionKeyContext
{
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    private readonly ICurrentUserService _currentUser;
    private readonly IBranchScopeState? _scopeState;

    public WorkshopDbContext(
        DbContextOptions<WorkshopDbContext> options,
        ICurrentUserService currentUser,
        IBranchScopeState? scopeState = null)
        : base(options)
    {
        _currentUser = currentUser;
        _scopeState = scopeState;
    }

    // Used by branch-scope query filters. EF Core re-evaluates per query.
    //   - Admin: defaults to null (see all), but if the user picked a branch
    //     in the app-bar selector, scope to that one.
    //   - Non-admin: always locked to their assigned BranchId. The override
    //     is ignored — non-admins shouldn't see the picker anyway, but
    //     defense in depth.
    //   - Anonymous / portal user / migrations / tests: null (= see all).
    public Guid? CurrentScopeBranchId
    {
        get
        {
            if (_currentUser.IsInRole(RoleNames.Admin))
                return _scopeState?.OverrideBranchId;
            return _currentUser.BranchId;
        }
    }

    // Shared
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<InsuranceCompany> InsuranceCompanies => Set<InsuranceCompany>();
    public DbSet<Assessor> Assessors => Set<Assessor>();
    public DbSet<Adjuster> Adjusters => Set<Adjuster>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<BodyPanel> BodyPanels => Set<BodyPanel>();
    public DbSet<BodyPanelOperation> BodyPanelOperations => Set<BodyPanelOperation>();
    public DbSet<ServiceCatalog> ServiceCatalogs => Set<ServiceCatalog>();

    // Insurance flow
    public DbSet<InsuranceCase> InsuranceCases => Set<InsuranceCase>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<InsurancePartLine> InsurancePartLines => Set<InsurancePartLine>();
    public DbSet<InsuranceApproval> InsuranceApprovals => Set<InsuranceApproval>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Repair> Repairs => Set<Repair>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Retail flow
    public DbSet<RetailCase> RetailCases => Set<RetailCase>();
    public DbSet<RetailPartLine> RetailPartLines => Set<RetailPartLine>();
    public DbSet<RetailRepair> RetailRepairs => Set<RetailRepair>();
    public DbSet<RetailCasePanel> RetailCasePanels => Set<RetailCasePanel>();

    // Cross-cutting
    public DbSet<CaseEvent> CaseEvents => Set<CaseEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<LoginAuditEntry> LoginAuditEntries => Set<LoginAuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(WorkshopDbContext).Assembly);
        ApplySoftDeleteQueryFilters(builder);
        ApplyBranchScopeQueryFilters(builder);
    }

    // Auto-filter IsDeleted = true on every entity inheriting from Entity.
    // Note: filters apply to the ROOT of a query; soft-deleted rows reached via
    // navigations/includes still come through. Filter explicitly on those when needed,
    // or call .IgnoreQueryFilters() to bypass entirely.
    private static void ApplySoftDeleteQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
                continue;

            var param = Expression.Parameter(entityType.ClrType, "e");
            var prop = Expression.PropertyOrField(param, nameof(Entity.IsDeleted));
            var notDeleted = Expression.Not(prop);
            var lambda = Expression.Lambda(notDeleted, param);
            builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    // Branch scoping: REPLACES the soft-delete-only filter on root aggregates that
    // carry a BranchId. HasQueryFilter is replace-only in EF Core, so the predicate
    // here re-includes the soft-delete check.
    //
    // Scope: only top-level aggregates (InsuranceCase, RetailCase, Warehouse) are
    // filtered. Child entities (Assessment, Repair, Payment, Photo, etc.) inherit
    // scoping when fetched through their parent's navigation; direct queries on
    // them (e.g. dashboard KPIs like db.Repairs.Count()) still see cross-branch
    // rows. Add per-child filters via parent-nav joins when a leak shows up.
    private void ApplyBranchScopeQueryFilters(ModelBuilder builder)
    {
        builder.Entity<InsuranceCase>().HasQueryFilter(c =>
            !c.IsDeleted &&
            (CurrentScopeBranchId == null || c.BranchId == CurrentScopeBranchId));

        builder.Entity<RetailCase>().HasQueryFilter(c =>
            !c.IsDeleted &&
            (CurrentScopeBranchId == null || c.BranchId == CurrentScopeBranchId));

        builder.Entity<Warehouse>().HasQueryFilter(w =>
            !w.IsDeleted &&
            (CurrentScopeBranchId == null || w.BranchId == CurrentScopeBranchId));
    }
}
