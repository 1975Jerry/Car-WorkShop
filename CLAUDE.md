# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

Paint Bull — a multi-branch insurance-repair + retail workflow platform. .NET 10 Blazor Server, PostgreSQL 16, MudBlazor UI. Replaces an Excel-based workflow with a single web app that also exposes three external portals (customer, insurer, supplier). See `DOMAIN-MODEL.md` for the entity model and the 12-stage InsuranceCase state machine — it is the source of truth for domain shape and workflow rules. `README.md` covers operational setup (Docker, seeding, default admin).

## Commands

```bash
# Build the whole solution
dotnet build CARWorshoNew.slnx

# Run unit tests — xUnit v3 uses Microsoft Testing Platform; `dotnet test` reports zero tests.
# Use `dotnet run` against each test project instead.
dotnet run --project tests/Workshop.Domain.Tests
dotnet run --project tests/Workshop.Application.Tests

# Run the web app (migrations + seed apply automatically on startup)
dotnet run --project src/Workshop.Web

# Add a new EF migration
dotnet ef migrations add <Name> \
  -p src/Workshop.Infrastructure/Workshop.Infrastructure.csproj \
  -o Persistence/Migrations

# Bring up the dev database (Postgres on :5432, pgAdmin on :5050)
docker compose up -d
```

**SDK pin — do not bump past 10.0.2xx casually.** `global.json` pins `10.0.203` with `rollForward: latestPatch`. SDK 10.0.300 dropped Blazor framework JS extraction from publish output, which breaks `/_framework/blazor.web.js` serving (the script is hardcoded in `Components/App.razor`). The Dockerfile has a build-time assertion that fails the image if neither the static-web-asset manifest route nor a physical `wwwroot/_framework/blazor.web.js` fallback exists — a green local build can still produce a broken container on a newer SDK. If you need to move to 10.0.3xx+, re-validate the manifest assertion in `Dockerfile` first.

**WSL-on-Windows DB note:** when Docker Desktop isn't running and Postgres is installed on Windows, WSL's `localhost` does not bridge to it. Use the host-gateway IP. To override the connection string at runtime:

```bash
ConnectionStrings__WorkshopDb="Host=<host-ip>;Port=5432;Database=workshop_dev;Username=...;Password=..." \
  dotnet run --project src/Workshop.Web --launch-profile http
```

**Dev auth is intentionally permissive.** Seeded admin is `admin@paintbull.local` / `123456`. On every startup `SeedRunner` (a) clears `AccessFailedCount` / `LockoutEnd` / `LockoutEnabled` on every user via `ClearAllLockoutsAsync` and (b) resets every user's password back to `123456` via `ResetAllPasswordsToDefaultAsync`. Account lockout is disabled (`Lockout.AllowedForNewUsers = false`, `lockoutOnFailure: false` in `Login.razor`) and the password policy is relaxed to 6 chars, no upper/lower/digit requirements. Do not deploy as-is — these are dev/internal defaults.

## Solution layout (clean architecture, 4 projects + 2 test projects)

| Project | Role |
|---|---|
| `Workshop.Domain` | Entities, enums, value objects, `InsuranceCaseStateMachine` (Stateless 5.x). No EF, no messaging. |
| `Workshop.Application` | Commands/queries/handlers (custom in-house dispatcher, see below), FluentValidation validators, DTOs, abstractions (`IWorkshopDbContext`, `IEmailSender`, `ISmsSender`, `IMyDataClient`, `IFileStore`). |
| `Workshop.Infrastructure` | `WorkshopDbContext` (Identity + domain combined), migrations, seed runner, stub adapters. |
| `Workshop.Web` | The single Blazor Server app. Serves staff UI + customer/insurer/supplier portals from the same process. No REST API project — there used to be a `Workshop.Api`; it was deleted. External integration endpoints (AADE/payment webhooks) must live here when added, likely under `/webhooks/*`. |

## Architecture — big picture

**Single Blazor Server app, four audiences, one DbContext.** Razor components call `Mediator.Send(...)` directly — no controllers, no REST hop. Each "portal" is a route group with its own layout and authorization policy:

| Audience | Route prefix | Layout | Auth policy |
|---|---|---|---|
| Staff | `/cases/*`, `/customers`, `/vehicles`, `/admin/*`, etc. | `MainLayout.razor` | `[Authorize]` (any Staff role) |
| Customer | `/portal/*` | `PortalLayout.razor` | `[Authorize(Policy = "CustomerPortal")]` |
| Insurer | `/insurer/*` | `InsurerLayout.razor` | `[Authorize(Policy = "InsurerPortal")]` |
| Supplier | `/supplier/*` | `SupplierLayout.razor` | `[Authorize(Policy = "SupplierPortal")]` |

Policies require a `PortalAudience` claim, stamped by `PortalClaimsPrincipalFactory` (which also stamps `BranchId`, `CustomerId`, `InsuranceCompanyId`, `SupplierId` into the auth cookie). Pages do **not** branch on audience at runtime; routing + layout + policy do the work.

**Custom in-house dispatcher, not MediatR.** Lives at `src/Workshop.Application/Common/Messaging/`. Same surface as MediatR (`IMediator`, `IRequest<T>`, `IRequestHandler<T,R>`, `IPipelineBehavior<T,R>`) plus a non-generic `IRequest` / `IRequestHandler<T>` form bridged to `Unit` via a default interface method, so void commands keep their `Task Handle(...)` signature. Wired up in `Workshop.Application/DependencyInjection.cs` with `AddWorkshopMediator(assembly)` (auto-scans handlers) and `AddPipelineBehavior(typeof(X<,>))`. The MediatR NuGet package was removed — don't reintroduce it.

**Pipeline order:** `SerializeRequestsBehavior` → `LoggingBehavior` → `ValidationBehavior` → handler. Serialize holds a per-DI-scope `SemaphoreSlim` because `WorkshopDbContext` is circuit-scoped in Blazor Server and components fire `Mediator.Send` concurrently during a single render pass — without serialization EF throws "A second operation was started on this context instance". Handlers stay lock-free. Logging emits DEBUG on success, WARN for slow (>500ms) and `ValidationException`, ERROR for everything else.

**Error handling.** Every layout wraps `@Body` in `PageErrorBoundary` (a thin wrapper around `<ErrorBoundary>` that auto-`Recover()`s on `NavigationManager.LocationChanged` so a render-time exception on page A doesn't poison page B). `MudTable.ServerData` delegates do NOT bubble into the boundary — guard those at the call site with try/catch, log via `ILogger`, surface via `Snackbar.Add(..., Severity.Error)`, and return empty `TableData` so the spinner stops (see `InsuranceCasesList.razor` for the pattern). `app.UseExceptionHandler("/error")` lands on the static-rendered `Error.razor`. `CircuitOptions.DetailedErrors = true` in Development only.

**Data protection keys persist to Postgres.** `WorkshopDbContext` implements `IDataProtectionKeyContext`; `Program.cs` calls `AddDataProtection().PersistKeysToDbContext<WorkshopDbContext>().SetApplicationName("PaintBull")`. Without this, container restarts (e.g. Azure redeploys) lose the key ring and every auth/antiforgery cookie issued before the restart fails to decrypt. The `data_protection_keys` table is created by migration `AddDataProtectionKeys`.

**DbContext is Identity + Domain combined.** `WorkshopDbContext : IdentityDbContext<User, Role, Guid>, IWorkshopDbContext`. Two cross-cutting filters:
- Soft delete: every entity inheriting `Entity` gets an automatic `IsDeleted == false` `HasQueryFilter`, installed reflectively in `OnModelCreating`.
- Branch scoping: `InsuranceCase`, `RetailCase`, `Warehouse` have explicit `HasQueryFilter` predicates combining the soft-delete with role-based branch visibility — `BranchManager` sees only their `BranchId`; `Admin` and `BodyShopManager` see all. Replace-only semantics in EF Core mean every such filter must include the soft-delete predicate too.

**Snake-case naming convention** is applied via `UseSnakeCaseNamingConvention()` — EF auto-translates `CaseNumber` → `case_number`. Don't fight it.

**Audit fields** (`CreatedAt`, `UpdatedAt`, `CreatedById`, `UpdatedById`) are stamped by `AuditSaveChangesInterceptor`. The `AuditLog` entity exists in the schema but is not yet written — only `CaseEvent` records the workflow timeline today.

**State machine** (`Workshop.Domain/Workflows/InsuranceCaseStateMachine.cs`) is the only place transition guards live. Every "advance" button is gated server-side; UI surfaces the remaining blockers as a checklist. Twelve stages, guards documented in `DOMAIN-MODEL.md` §5.

**Localization:** `IStringLocalizer<SharedResource>` injected as `L`. Two resx files at `src/Workshop.Web/Resources/SharedResource.{en,el}.resx` — Greek is primary. Keys are at parity (~401 each). Always add to both when introducing a new key.

## Code conventions

- **No REST controllers.** Components call handlers directly via `Mediator.Send(...)`. If you find yourself reaching for an HTTP client, you're probably already inside the app.
- **Commands/queries are records** implementing `IRequest<T>` (or `IRequest` for void) from `Workshop.Application.Common.Messaging`, in `src/Workshop.Application/Features/<Entity>/`. Convention: `Create<Entity>Command`, `Update<Entity>Command`, `Delete<Entity>Command`, `Get<Entity>ByIdQuery`, `List<Entity>Query`. Each gets a handler and (for commands) a FluentValidation `AbstractValidator` in the same file.
- **MudBlazor 9.4** is the only UI kit. Dialogs use `IDialogService` (`Dialogs.ShowAsync<TDialog>(...)`); stepper wizards use `MudStepper` with `OnPreviewInteraction` for gating between steps (see `NewInsuranceCase.razor` and `NewRetailCase.razor`).
- **Reusable dialogs return data, not bool.** Pattern: `Dialog.Close(DialogResult.Ok(<newGuid or domain DTO>))` so callers can chain on the result. `CustomerEditDialog`, `VehicleEditDialog` follow this.
- **Lookup queries** in `Workshop.Application/Common/Lookups/LookupQueries.cs` return `LookupItem(Guid Id, string Label)` for autocompletes. Keep them lean (typeahead helpers — don't widen their search predicate).
- **List query handlers** accept a single `string? Search` plus discrete filters. Search is OR-ed across many entity columns server-side (Postgres `ILIKE`-equivalent via `.ToLower().Contains(s)`). Cross-entity nav predicates (`v.Customer.LastName.Contains(s)`) are fine; cross-entity nav **projections** trip the EF InMemory provider and silently return zero rows — project the FK and resolve the label separately when InMemory matters.
- **Don't extract a shared "step component"** between the Insurance and Retail wizards yet — field sets diverge enough that abstraction would carry too many parameters. Premature.

## Testing

Two xUnit v3 test projects. Run via `dotnet run` (Microsoft Testing Platform — `dotnet test` will find zero tests). Application tests use the **EF Core InMemory** provider; Domain tests are pure (state machine only). When adding a handler test that touches navigation properties in a `Select(...)` projection, be aware InMemory may return zero rows where Postgres returns the expected data — work around by projecting the FK and looking up the label separately.

## External integrations

All external integrations ship as logging-only stubs today:
- `IEmailSender`, `ISmsSender` — print payload to logs.
- `IMyDataClient` — returns deterministic fake MARKs.
- `IFileStore` — local filesystem.

Swap real implementations by re-registering in `Workshop.Infrastructure/DependencyInjection.cs`. Webhook receivers (AADE async confirmations, payment gateway notifications) are not wired — add endpoints inside `Workshop.Web` (this repo no longer has a separate API project).

## Known structural gaps (see README "tech debt" for full list)

- No background-job runner (Hangfire is listed but not wired). Reminders, batched notifications, and scheduled myDATA submission have no host yet.
- Account management UI is bare — only login/logout. No password reset / MFA / staff admin / role assignment UI. The seed-time password reset is the de-facto admin tool.
- `AuditSaveChangesInterceptor` stamps timestamps only; `AuditLog` rows aren't written.
- Only the Quote PDF (QuestPDF) template exists; Invoice / Receipt / Case Form / Insurance Form PDFs are upload-only.
- `PartCatalog` taxonomy from `ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ.docx` (~400 rows) is not seeded — `PartLine.PartName` is free-text today.
