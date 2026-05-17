# Paint Bull — Workshop Management Platform

Multi-branch insurance-repair + retail workflow platform for Paint Bull body shop. Built on .NET 10 Blazor Server + MudBlazor + PostgreSQL.

See [DOMAIN-MODEL.md](DOMAIN-MODEL.md) for the entity model, workflow, and portal access matrix.

## Prerequisites

- **.NET 10 SDK** (verified working with 10.0.203)
- **PostgreSQL 16** — either via Docker or local install
- **Node** is not required (Blazor Server uses MudBlazor's compiled assets)

## Setup — first run

### 1. Start PostgreSQL

**Option A — Docker (recommended):**

If `docker` isn't available in your WSL distro, first enable Docker Desktop WSL integration:
*Settings → Resources → WSL Integration → enable for your distro*.

```bash
docker compose up -d
```

This starts:
- Postgres 16 on `localhost:5432` (db: `workshop_dev`, user: `workshop`, password: `workshop`)
- pgAdmin on `http://localhost:5050` (login: `admin@paintbull.local` / `admin`)

**Option B — Local Postgres install:**

Create a database matching `appsettings.json`:
```sql
CREATE USER workshop WITH PASSWORD 'workshop';
CREATE DATABASE workshop_dev OWNER workshop;
```

### 2. Apply migrations + seed

Migrations and seed data are applied automatically on first `Workshop.Web` startup. The seed includes:
- 5 staff roles (Admin, BranchManager, Receptionist, Technician, BodyShopManager)
- 1 default admin user (password auto-generated and **printed to console — copy it before it scrolls past**)
- Paint Bull company profile
- Default branch (Ταύρος) + warehouse
- 10 Greek insurance companies
- 5 service-catalog entries
- 78 body panels + 302 allowed-operation rows (from `ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ2.xlsx`)

### 3. Run the staff app

```bash
dotnet run --project src/Workshop.Web
```

Open `https://localhost:5001` (port varies — check console output).

### 4. Portals

The customer, insurer, and supplier portals are served by `Workshop.Web` itself under `/portal`, `/insurer`, and `/supplier`. Sign in as a user whose `PortalAudience` matches — the layout and route guards switch automatically. Single-app architecture is intentional: Blazor Server calls MediatR handlers directly with no per-portal SPA or REST hop.

## Common tasks

```bash
# Build all
dotnet build CARWorshoNew.slnx

# Run unit tests — xUnit v3 runs via `dotnet run`, not `dotnet test`.
# `dotnet test` reports zero tests because of the xUnit v3 Microsoft Testing Platform integration.
dotnet run --project tests/Workshop.Domain.Tests
dotnet run --project tests/Workshop.Application.Tests

# Add a new EF migration
dotnet ef migrations add <Name> \
  -p src/Workshop.Infrastructure/Workshop.Infrastructure.csproj \
  -o Persistence/Migrations

# Apply migrations manually (Web normally does this on startup)
dotnet ef database update \
  -p src/Workshop.Infrastructure/Workshop.Infrastructure.csproj
```

## Solution layout

| Project | Role |
|---|---|
| `Workshop.Domain` | Entities, enums, value objects, `InsuranceCaseStateMachine` |
| `Workshop.Application` | Use cases, validators, DTOs, notification + myDATA abstractions |
| `Workshop.Infrastructure` | EF Core `WorkshopDbContext`, Identity, file storage, seed runner, stub adapters (email/SMS/myDATA) |
| `Workshop.Web` | Single Blazor Server app — staff UI plus the customer (`/portal`), insurer (`/insurer`), and supplier (`/supplier`) portals |
| `tests/Workshop.Domain.Tests` | xUnit v3 tests — state machine (14) |
| `tests/Workshop.Application.Tests` | xUnit v3 tests — application use cases, dispatchers, myDATA (157) |

## Configuration

`src/Workshop.Web/appsettings.json` — default connection string and logging.
`src/Workshop.Web/appsettings.Development.json` — local overrides.
Sensitive overrides: `appsettings.Local.json` (gitignored), `dotnet user-secrets`, or env vars.

Override the connection string via env var:
```bash
export WORKSHOP_CONNECTION_STRING="Host=...;Port=...;Database=...;Username=...;Password=..."
```

## Phase status

All 11 phases from [DOMAIN-MODEL.md §9](DOMAIN-MODEL.md) are in. External-integration phases ship as abstractions with logging/stub adapters — swap the DI registrations in `Workshop.Infrastructure/DependencyInjection.cs` to plug real providers.

| Phase | Status | Notes |
|---|---|---|
| 0 — Scaffold, schema, state machine, seed | ✅ | |
| 1 — Reference data CRUD | ✅ | Customers, Vehicles, Branches, Insurance Companies, Assessors, Adjusters, Suppliers |
| 2 — InsuranceCase + state machine UI | ✅ | |
| 3 — Assessment + clickable SVG damage diagram + WorkItems | ✅ | |
| 4 — Insurance Approval + Customer Assignment | ✅ | |
| 5 — Parts module + Supplier portal | ✅ | Multi-state receipt, branch routing, warehouse |
| 6 — Repair scheduling, technician, completion | ✅ | |
| 7 — Documents + Photos | ✅ | Photos cover Assessment / Repair / RetailRepair |
| 8 — Settlement + Payment + Quote PDF | ✅ | QuestPDF; only Quote template — Invoice/Receipt PDFs pending |
| 9 — Retail flow | ✅ | Parallel aggregate to Insurance |
| 10 — Dashboard / reports | ✅ | KPIs on Home; `/reports` for branch breakdown, aging, parts variance, technician productivity |
| 11 — Notifications, myDATA, reports | ✅ stubs | Logging email/SMS senders + stub `IMyDataClient`; in-app bell wired |

## Known issues / tech debt

- **External integrations are stubs.** `IEmailSender`, `ISmsSender`, `IMyDataClient`, and `IFileStore` need real adapters (SMTP/SendGrid, Twilio/Vonage, AADE sandbox, S3/Azure Blob).
- **Webhook receivers are not yet wired.** When the real AADE adapter and a payment gateway land, their inbound webhooks (cancellations, asynchronous MARK confirmations, payment notifications) need endpoints inside `Workshop.Web` — likely under `/webhooks/*`.
- **No background-job runner.** Hangfire is listed in the stack but not wired — needed for reminders (vehicle insurance expiration), notification batching, and scheduled myDATA submissions.
- **Account management is bare.** Only login/logout pages exist — no password reset, email confirmation, MFA enrolment, profile, or staff/user admin page. Identity tables already carry the columns.
- **Audit gap.** `AuditLog` entity exists and is in the schema, but `AuditSaveChangesInterceptor` only stamps `Created/UpdatedAt/By` — it does not write `AuditLog` rows yet. Only `CaseEvent` (workflow transitions) is audited.
- **`PartCatalog` not modelled.** The hierarchical parts taxonomy from `ΜΕΡΗ ΑΥΤΟΚΙΝΗΤΟΥ.docx` (~400 rows) is not seeded — `PartLine.PartName` remains free-text.
- **Only the Quote PDF template exists.** Invoice / Receipt / Case Form / Insurance Form PDFs referenced by `DocumentType` are upload-only today.
- **Stateless `PermittedTriggers` is obsolete.** Migrate to `PermittedTriggersAsync`.
