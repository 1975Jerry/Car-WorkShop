using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Workshop.Domain.Entities.Identity;

namespace Workshop.Web.Services;

/// <summary>
/// Stamps PortalAudience, CustomerId, BranchId, InsuranceCompanyId, SupplierId into the
/// auth cookie so the rest of the app can read them without touching the db.
/// </summary>
public class PortalClaimsPrincipalFactory(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User, Role>(userManager, roleManager, options)
{
    public const string PortalAudienceClaim = "portal_audience";
    public const string CustomerIdClaim = "customer_id";
    public const string BranchIdClaim = "branch_id";
    public const string InsuranceCompanyIdClaim = "insurance_company_id";
    public const string SupplierIdClaim = "supplier_id";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var id = await base.GenerateClaimsAsync(user);
        id.AddClaim(new Claim(PortalAudienceClaim, user.PortalAudience.ToString()));
        if (user.CustomerId is { } cid) id.AddClaim(new Claim(CustomerIdClaim, cid.ToString()));
        if (user.BranchId is { } bid) id.AddClaim(new Claim(BranchIdClaim, bid.ToString()));
        if (user.InsuranceCompanyId is { } ic) id.AddClaim(new Claim(InsuranceCompanyIdClaim, ic.ToString()));
        if (user.SupplierId is { } sid) id.AddClaim(new Claim(SupplierIdClaim, sid.ToString()));
        return id;
    }
}
