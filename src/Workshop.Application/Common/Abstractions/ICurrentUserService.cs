using Workshop.Domain.Enums;

namespace Workshop.Application.Common.Abstractions;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? BranchId { get; }
    Guid? CustomerId { get; }
    Guid? InsuranceCompanyId { get; }
    Guid? SupplierId { get; }
    PortalAudience? Audience { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    bool IsAnyOf(params string[] roles);
}
