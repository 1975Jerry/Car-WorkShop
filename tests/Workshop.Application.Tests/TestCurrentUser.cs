using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

internal class TestCurrentUser : ICurrentUserService
{
    /// <summary>Authenticated user (random Guid). Use Anonymous() for unauthenticated.</summary>
    public TestCurrentUser(Guid? branchId = null, params string[] roles)
    {
        UserId = Guid.CreateVersion7();
        BranchId = branchId;
        _roles = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private TestCurrentUser(bool _) { _roles = new HashSet<string>(); UserId = null; }

    public static TestCurrentUser Anonymous() => new(false);

    public static TestCurrentUser Customer(Guid customerId) =>
        new() { CustomerId = customerId, Audience = PortalAudience.Customer };

    private readonly HashSet<string> _roles;

    public Guid? UserId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? InsuranceCompanyId { get; init; }
    public Guid? SupplierId { get; init; }
    public PortalAudience? Audience { get; init; }
    public bool IsAuthenticated => UserId.HasValue;
    public bool IsInRole(string role) => _roles.Contains(role);
    public bool IsAnyOf(params string[] roles) => roles.Any(_roles.Contains);
}
