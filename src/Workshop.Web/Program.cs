using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
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

    // In Development surface real exception messages to the browser console so devs
    // can diagnose render/circuit failures without scraping server logs. In Production
    // the framework hides them behind a generic error.
    builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(o =>
    {
        o.DetailedErrors = builder.Environment.IsDevelopment();
    });

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

    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<Workshop.Infrastructure.Persistence.WorkshopDbContext>()
        .SetApplicationName("PaintBull");

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

    // Build-identifying banner so we can confirm in Azure logs which image is live.
    Log.Information("Workshop.Web boot: static-assets=MapStaticAssets, build-tag=blazor-static-assets-manifest-v1");

    // Diagnostic for framework asset routing. In published .NET 10 Blazor Web Apps,
    // /_framework/blazor.web.js is served from the static web asset endpoint manifest
    // by MapStaticAssets(); it does not need to exist under wwwroot/_framework.
    try
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Workshop.Web.staticwebassets.endpoints.json");
        int frameworkRoutes = 0;
        var hasBlazorWebRoute = false;
        if (File.Exists(manifestPath))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (doc.RootElement.TryGetProperty("Endpoints", out var endpoints))
            {
                foreach (var e in endpoints.EnumerateArray())
                {
                    if (e.TryGetProperty("Route", out var r) && r.GetString() is { } route
                        && route.StartsWith("_framework/", StringComparison.Ordinal))
                    {
                        frameworkRoutes++;
                        hasBlazorWebRoute |= route == "_framework/blazor.web.js";
                    }
                }
            }
        }
        Log.Information(
            "static-asset diagnostic: manifest={ManifestPath} exists={ManifestExists} framework-routes={FrameworkRoutes} blazor.web.js-route={HasBlazorWebRoute}",
            manifestPath, File.Exists(manifestPath), frameworkRoutes, hasBlazorWebRoute);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "static-asset diagnostic failed");
    }

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

    // Language toggle from MainLayout's globe icon — writes the
    // RequestLocalization culture cookie and bounces back to the page that
    // sent the user. Two-step (full reload) because the Blazor circuit caches
    // the IStringLocalizer's CultureInfo for the life of the circuit.
    app.MapGet("/api/culture", (string culture, string? redirectUri, HttpContext ctx) =>
    {
        var allowed = new[] { "el", "en" };
        if (!allowed.Contains(culture)) return Results.BadRequest("unsupported culture");
        var cookieValue = Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
            new Microsoft.AspNetCore.Localization.RequestCulture(culture));
        ctx.Response.Cookies.Append(
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
            cookieValue,
            new CookieOptions { Path = "/", Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
        var target = string.IsNullOrEmpty(redirectUri) || !redirectUri.StartsWith('/') ? "/" : redirectUri;
        return Results.LocalRedirect(target);
    });

    // MapStaticAssets serves manifest-backed framework/package assets. UseStaticFiles
    // above serves physical wwwroot files, including uploaded files and a physical
    // _framework fallback if the Docker publish output contains one.
    app.MapStaticAssets();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .WithStaticAssets();

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
