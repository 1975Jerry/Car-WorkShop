using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using Serilog;
using System.Globalization;
using Workshop.Application;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.Identity;
using Workshop.Infrastructure;
using Workshop.Infrastructure.Seeding;
using Workshop.Web.Components;
using Workshop.Web.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/workshop-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/workshop-.log", rollingInterval: RollingInterval.Day));

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices();
    builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");

    // Anchor the file store under wwwroot/uploads so files are served by the
    // existing static-file middleware. The relative path persisted on Photo/Document
    // rows is e.g. "uploads/photos/..." — see LocalFileStore.
    var uploadRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
    builder.Configuration["FileStore:Root"] = uploadRoot;

    builder.Services.AddWorkshopInfrastructure(builder.Configuration);
    builder.Services.AddWorkshopApplication();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
    builder.Services.AddScoped<IBranchScopeState, CircuitBranchScopeState>();

    // /health/live — process is up; /health/ready — DB reachable.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<Workshop.Infrastructure.Persistence.WorkshopDbContext>(
            name: "db", tags: ["ready"]);

    builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
        .AddIdentityCookies();
    builder.Services.ConfigureApplicationCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Login";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
    });
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("CustomerPortal", p => p.RequireAuthenticatedUser()
            .RequireClaim(Workshop.Web.Services.PortalClaimsPrincipalFactory.PortalAudienceClaim,
                Workshop.Domain.Enums.PortalAudience.Customer.ToString()))
        .AddPolicy("InsurerPortal", p => p.RequireAuthenticatedUser()
            .RequireClaim(Workshop.Web.Services.PortalClaimsPrincipalFactory.PortalAudienceClaim,
                Workshop.Domain.Enums.PortalAudience.Insurance.ToString()))
        .AddPolicy("SupplierPortal", p => p.RequireAuthenticatedUser()
            .RequireClaim(Workshop.Web.Services.PortalClaimsPrincipalFactory.PortalAudienceClaim,
                Workshop.Domain.Enums.PortalAudience.Supplier.ToString()));
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

    builder.Services.AddScoped<SignInManager<User>>();
    builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, PortalClaimsPrincipalFactory>();
    // Required by Cookie auth to revalidate the security stamp on each request.
    // Without this, every authenticated request returns 500 from CookieAuthenticationHandler.
    builder.Services.AddScoped<ISecurityStampValidator, SecurityStampValidator<User>>();
    builder.Services.AddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<User>>();

    var app = builder.Build();

    // Apply migrations + seed on startup. Idempotent — safe to run every time.
    // Prefer SEED_DIR env var (e.g. inside Docker), else fall back to a sibling
    // ./seed folder, else the source-tree convention used in local development.
    var seedDir = builder.Configuration["SEED_DIR"]
        ?? (Directory.Exists(Path.Combine(builder.Environment.ContentRootPath, "seed"))
            ? Path.Combine(builder.Environment.ContentRootPath, "seed")
            : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "seed")));
    await app.Services.SeedAsync(seedDir);

    var supportedCultures = new[] { new CultureInfo("el"), new CultureInfo("en") };
    app.UseRequestLocalization(new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
    {
        DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("el"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapHealthChecks("/health/live", new()
    {
        Predicate = _ => false // liveness: only assert the process is up
    });
    app.MapHealthChecks("/health/ready", new()
    {
        Predicate = c => c.Tags.Contains("ready")
    });

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Workshop.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
