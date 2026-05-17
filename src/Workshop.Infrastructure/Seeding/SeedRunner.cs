using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Shared;
using Workshop.Domain.Enums;
using Workshop.Infrastructure.Persistence;

namespace Workshop.Infrastructure.Seeding;

public partial class SeedRunner
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly WorkshopDbContext _db;
    private readonly UserManager<User> _users;
    private readonly RoleManager<Role> _roles;
    private readonly ILogger<SeedRunner> _log;
    private readonly string _seedDir;

    public SeedRunner(WorkshopDbContext db, UserManager<User> users, RoleManager<Role> roles,
                      ILogger<SeedRunner> log, string seedDir)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _log = log;
        _seedDir = seedDir;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);

        await SeedRolesAsync();
        await SeedAdminUserAsync();
        await SeedCompanyProfileAsync(ct);
        await SeedBranchesAsync(ct);
        await SeedInsuranceCompaniesAsync(ct);
        await SeedServiceCatalogAsync(ct);
        await SeedBodyPanelsAsync(ct);
        await BackfillBodyPanelDiagramCoordsAsync(ct);

        // Order matters: reference rows above must exist first so portal users can FK them.
        await _db.SaveChangesAsync(ct);
        await SeedDemoPortalUsersAsync(ct);
        await _db.SaveChangesAsync(ct);
        await SeedDemoCasesAsync(ct);
        await RebalanceDemoCasesAcrossBranchesAsync(ct);
        await BackfillDemoCaseEventsAsync(ct);
        _log.LogInformation("Seed complete.");
    }

    private async Task SeedDemoPortalUsersAsync(CancellationToken ct)
    {
        // Customer portal — needs a Customer entity to FK to.
        var demoCustomer = await _db.Customers
            .FirstOrDefaultAsync(c => c.MobilePhone == "6900000000", ct);
        if (demoCustomer is null)
        {
            demoCustomer = new Customer
            {
                CustomerType = CustomerType.Individual,
                FirstName = "Demo", LastName = "Customer",
                MobilePhone = "6900000000",
                Email = "customer@demo.local",
                GdprConsent = true, GdprConsentAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.Customers.Add(demoCustomer);
            await _db.SaveChangesAsync(ct);
        }
        await CreatePortalUserAsync(
            email: "customer@demo.local",
            fullName: "Demo Customer",
            audience: PortalAudience.Customer,
            customerId: demoCustomer.Id);

        // Insurance portal — needs an InsuranceCompany. Reuse the first seeded one.
        var insurer = await _db.InsuranceCompanies.OrderBy(c => c.Name).FirstOrDefaultAsync(ct);
        if (insurer is not null)
        {
            await CreatePortalUserAsync(
                email: "insurer@demo.local",
                fullName: $"Demo Adjuster ({insurer.Name})",
                audience: PortalAudience.Insurance,
                insuranceCompanyId: insurer.Id);
        }
        else
        {
            _log.LogWarning("No insurance companies seeded; skipping demo insurer portal user.");
        }

        // Supplier portal — needs a Supplier entity. Create a demo Supplier if absent.
        var demoSupplier = await _db.Suppliers
            .FirstOrDefaultAsync(s => s.Name == "Demo Supplier", ct);
        if (demoSupplier is null)
        {
            demoSupplier = new Supplier
            {
                Name = "Demo Supplier",
                Phone = "2100000000",
                Email = "supplier@demo.local",
                IsActive = true
            };
            _db.Suppliers.Add(demoSupplier);
            await _db.SaveChangesAsync(ct);
        }
        await CreatePortalUserAsync(
            email: "supplier@demo.local",
            fullName: "Demo Supplier User",
            audience: PortalAudience.Supplier,
            supplierId: demoSupplier.Id);
    }

    private async Task CreatePortalUserAsync(
        string email, string fullName, PortalAudience audience,
        Guid? customerId = null, Guid? insuranceCompanyId = null, Guid? supplierId = null)
    {
        if (await _users.FindByEmailAsync(email) is not null) return;

        var password = GenerateRandomPassword();
        var user = new User
        {
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            FullName = fullName,
            PortalAudience = audience,
            CustomerId = customerId,
            InsuranceCompanyId = insuranceCompanyId,
            SupplierId = supplierId,
            Language = "el",
            IsActive = true
        };
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            _log.LogError("Failed to create demo {audience} user: {errors}",
                audience, string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        _log.LogWarning("====================================================================");
        _log.LogWarning("  Demo {audience} portal credentials (CHANGE OR DELETE IN PROD):", audience);
        _log.LogWarning("    email:    {email}", email);
        _log.LogWarning("    password: {password}", password);
        _log.LogWarning("====================================================================");
    }

    private async Task SeedRolesAsync()
    {
        foreach (var name in RoleNames.AllStaffRoles)
        {
            if (!await _roles.RoleExistsAsync(name))
                await _roles.CreateAsync(new Role(name));
        }
    }

    private async Task SeedAdminUserAsync()
    {
        const string adminEmail = "admin@paintbull.local";
        if (await _users.FindByEmailAsync(adminEmail) is not null) return;

        var password = GenerateRandomPassword();
        var admin = new User
        {
            Email = adminEmail,
            UserName = adminEmail,
            EmailConfirmed = true,
            FullName = "Default Admin",
            PortalAudience = PortalAudience.Staff,
            Language = "el",
            IsActive = true
        };

        var result = await _users.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            _log.LogError("Failed to create admin user: {errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }
        await _users.AddToRoleAsync(admin, RoleNames.Admin);

        _log.LogWarning("====================================================================");
        _log.LogWarning("  Initial admin credentials (CHANGE ON FIRST LOGIN):");
        _log.LogWarning("    email:    {email}", adminEmail);
        _log.LogWarning("    password: {password}", password);
        _log.LogWarning("====================================================================");
    }

    private async Task SeedCompanyProfileAsync(CancellationToken ct)
    {
        if (await _db.CompanyProfiles.AnyAsync(ct)) return;
        var data = await LoadAsync<CompanyProfileSeed>("company-profile.json");
        if (data is null) return;
        _db.CompanyProfiles.Add(new CompanyProfile
        {
            Name = data.Name,
            AddressLine = data.AddressLine,
            City = data.City,
            PostalCode = data.PostalCode,
            Phone = data.Phone,
            Email = data.Email,
            VatNumber = data.VatNumber,
            TaxOffice = data.TaxOffice,
            LogoPath = data.LogoPath,
            DefaultVatRate = data.DefaultVatRate
        });
    }

    private async Task SeedBranchesAsync(CancellationToken ct)
    {
        // Upsert by Code so adding a branch to branches.json shows up on the next
        // boot even when the table is already populated. User edits to existing
        // branches via the admin UI are preserved.
        var data = await LoadAsync<List<BranchSeed>>("branches.json") ?? new();
        if (data.Count == 0) return;

        var existingCodes = await _db.Branches
            .Where(b => data.Select(d => d.Code).Contains(b.Code))
            .Select(b => b.Code).ToListAsync(ct);

        foreach (var b in data.Where(d => !existingCodes.Contains(d.Code)))
        {
            var branch = new Branch
            {
                Name = b.Name,
                Code = b.Code,
                AddressLine = b.AddressLine,
                City = b.City,
                PostalCode = b.PostalCode,
                Phone = b.Phone,
                IsActive = true
            };
            branch.Warehouse = new Warehouse
            {
                Name = b.Warehouse.Name,
                Description = b.Warehouse.Description
            };
            _db.Branches.Add(branch);
        }
    }

    private async Task SeedInsuranceCompaniesAsync(CancellationToken ct)
    {
        if (await _db.InsuranceCompanies.AnyAsync(ct)) return;
        var data = await LoadAsync<List<InsuranceCompanySeed>>("insurance-companies.json") ?? new();
        foreach (var c in data)
        {
            _db.InsuranceCompanies.Add(new InsuranceCompany
            {
                Name = c.Name,
                Phone = c.Phone,
                Email = c.Email,
                AddressLine = c.AddressLine,
                VatNumber = c.VatNumber,
                IsActive = true
            });
        }
    }

    private async Task SeedServiceCatalogAsync(CancellationToken ct)
    {
        if (await _db.ServiceCatalogs.AnyAsync(ct)) return;
        var data = await LoadAsync<List<ServiceCatalogSeed>>("service-catalog.json") ?? new();
        foreach (var s in data)
        {
            _db.ServiceCatalogs.Add(new ServiceCatalog
            {
                Code = s.Code,
                NameGr = s.NameGr,
                NameEn = s.NameEn,
                DefaultPrice = s.DefaultPrice,
                IsActive = true
            });
        }
    }

    private async Task SeedBodyPanelsAsync(CancellationToken ct)
    {
        if (await _db.BodyPanels.AnyAsync(ct)) return;
        var panels = await LoadAsync<List<BodyPanelSeed>>("body-panels.json") ?? new();
        var ops = await LoadAsync<List<BodyPanelOperationSeed>>("body-panel-operations.json") ?? new();

        var entities = new Dictionary<string, BodyPanel>();
        foreach (var p in panels)
        {
            var panel = new BodyPanel
            {
                Code = p.Code,
                DescriptionGr = p.DescriptionGr,
                DescriptionEn = p.DescriptionEn,
                Category = Enum.Parse<BodyPanelCategory>(p.Category),
                Side = Enum.Parse<PanelSide>(p.Side),
                DiagramX = p.DiagramX,
                DiagramY = p.DiagramY,
                IsActive = true
            };
            entities[p.Code] = panel;
            _db.BodyPanels.Add(panel);
        }

        foreach (var op in ops)
        {
            if (!entities.TryGetValue(op.PanelCode, out var panel))
            {
                _log.LogWarning("Allowed-op references unknown panel '{code}'; skipping.", op.PanelCode);
                continue;
            }
            panel.AllowedOperations.Add(new BodyPanelOperation
            {
                BodyPanel = panel,
                Operation = Enum.Parse<OperationType>(op.Operation)
            });
        }
    }

    private async Task BackfillBodyPanelDiagramCoordsAsync(CancellationToken ct)
    {
        // Existing DBs were seeded before diagram coords existed in body-panels.json.
        // Only fill in rows still missing coords so manual tweaks stay intact.
        var missing = await _db.BodyPanels
            .Where(p => p.DiagramX == null && p.DiagramY == null)
            .ToListAsync(ct);
        if (missing.Count == 0) return;

        var seeds = await LoadAsync<List<BodyPanelSeed>>("body-panels.json") ?? new();
        var byCode = seeds
            .Where(s => s.DiagramX.HasValue && s.DiagramY.HasValue)
            .ToDictionary(s => s.Code, s => (s.DiagramX!.Value, s.DiagramY!.Value));

        var updated = 0;
        foreach (var panel in missing)
        {
            if (!byCode.TryGetValue(panel.Code, out var xy)) continue;
            panel.DiagramX = xy.Item1;
            panel.DiagramY = xy.Item2;
            updated++;
        }
        if (updated > 0)
        {
            _log.LogInformation("Backfilled diagram coordinates on {count} body panels.", updated);
        }
    }

    private async Task<T?> LoadAsync<T>(string fileName)
    {
        var path = Path.Combine(_seedDir, fileName);
        if (!File.Exists(path))
        {
            _log.LogWarning("Seed file not found: {path}", path);
            return default;
        }
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOpts);
    }

    private static string GenerateRandomPassword(int length = 16)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%";
        var bytes = RandomNumberGenerator.GetBytes(length);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray()) + "Aa1!";
    }
}

public static class SeedExtensions
{
    public static async Task SeedAsync(this IServiceProvider services, string seedDir, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var runner = new SeedRunner(
            sp.GetRequiredService<WorkshopDbContext>(),
            sp.GetRequiredService<UserManager<User>>(),
            sp.GetRequiredService<RoleManager<Role>>(),
            sp.GetRequiredService<ILogger<SeedRunner>>(),
            seedDir);
        await runner.RunAsync(ct);
    }
}
