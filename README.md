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

### 4. Other apps

```bash
dotnet run --project src/Workshop.Api               # REST API for external portals
dotnet run --project src/Workshop.Portal.Customer   # Customer portal
dotnet run --project src/Workshop.Portal.Insurance  # Insurance reviewer portal
dotnet run --project src/Workshop.Portal.Supplier   # Supplier portal
```

## Common tasks

```bash
# Build all
dotnet build CARWorshoNew.slnx

# Run unit tests (state machine + future application tests)
dotnet test

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
| `Workshop.Application` | Use cases, validators, DTOs (Phase 1+) |
| `Workshop.Infrastructure` | EF Core `WorkshopDbContext`, Identity, file storage, seed runner |
| `Workshop.Web` | Main Blazor Server staff app — MudBlazor + i18n (EL/EN) |
| `Workshop.Portal.Customer` | Customer self-service portal |
| `Workshop.Portal.Insurance` | Insurance company reviewer portal |
| `Workshop.Portal.Supplier` | Supplier order portal |
| `Workshop.Api` | Shared REST API for the 3 external portals |
| `tests/Workshop.Domain.Tests` | xUnit 3 tests — state machine (14 passing) |
| `tests/Workshop.Application.Tests` | Application use-case tests (Phase 1+) |

## Configuration

`src/Workshop.Web/appsettings.json` — default connection string and logging.
`src/Workshop.Web/appsettings.Development.json` — local overrides.
Sensitive overrides: `appsettings.Local.json` (gitignored), `dotnet user-secrets`, or env vars.

Override the connection string via env var:
```bash
export WORKSHOP_CONNECTION_STRING="Host=...;Port=...;Database=...;Username=...;Password=..."
```

## Phase status

**Phase 0 ✅ scaffold complete** — solution, schema, state machine, MudBlazor theme, EL/EN resources, seed runner.

Next: **Phase 1** — reference data CRUD (Customers, Vehicles, Branches, Insurance Companies, etc.).
See [DOMAIN-MODEL.md §9](DOMAIN-MODEL.md) for the full phase plan.

## Known issues / tech debt

- Stateless `PermittedTriggers` is marked obsolete — migrate to `PermittedTriggersAsync` later.
- Greek myDATA invoicing integration is Phase 11.
- Damage diagram interactive SVG (clickable panel selector) is Phase 3.
