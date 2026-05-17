using System.Security.Claims;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Web.Services;

public class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid? BranchId =>
        Guid.TryParse(User?.FindFirstValue(PortalClaimsPrincipalFactory.BranchIdClaim), out var id) ? id : null;

    public Guid? CustomerId =>
        Guid.TryParse(User?.FindFirstValue(PortalClaimsPrincipalFactory.CustomerIdClaim), out var id) ? id : null;

    public Guid? InsuranceCompanyId =>
        Guid.TryParse(User?.FindFirstValue(PortalClaimsPrincipalFactory.InsuranceCompanyIdClaim), out var id) ? id : null;

    public Guid? SupplierId =>
        Guid.TryParse(User?.FindFirstValue(PortalClaimsPrincipalFactory.SupplierIdClaim), out var id) ? id : null;

    public PortalAudience? Audience =>
        Enum.TryParse<PortalAudience>(User?.FindFirstValue(PortalClaimsPrincipalFactory.PortalAudienceClaim), out var a) ? a : null;

    public bool IsInRole(string role) => User?.IsInRole(role) == true;

    public bool IsAnyOf(params string[] roles) => roles.Any(IsInRole);
}
