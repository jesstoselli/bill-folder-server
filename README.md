# BillFolder

Personal finance API for Brazilians who want to actually *understand* their money cycle — not just track it. Built around a single hard question:

> **"How much do I have left until the end of this cycle?"**

A "cycle" isn't a calendar month. It's the BillFolder cycle — starts on the 5th business day of each month, runs to the day before the next 5th business day. Everything (expenses, income, card statements, savings adjustments) is aggregated against that cycle.

**Live API:** https://api.billfolder.app

---

## Stack

| Layer | Technology |
| --- | --- |
| Runtime | .NET 10 (LTS) |
| Web | ASP.NET Core 10 minimal APIs |
| ORM | EF Core 10 + Npgsql.EntityFrameworkCore.PostgreSQL |
| Database | PostgreSQL 16 |
| Auth | JWT Bearer (HS256) + refresh tokens with rotation |
| Password hashing | Argon2id (Konscious.Security.Cryptography) |
| Validation | FluentValidation 12 |
| Linting | Roslyn analyzers + Meziantou.Analyzer (treat warnings as errors) |
| Container | Docker (multi-stage Alpine, ~120 MB final image) |
| Reverse proxy | nginx + Let's Encrypt (auto-renewing TLS) |
| Hosting | Hetzner Cloud (single CX23 VPS, 4 GB RAM) |
| CI/CD | GitHub Actions: build, test, push to GHCR, SSH deploy |

Total infrastructure cost: about EUR 5/month.

---

## Architecture

Clean Architecture, four projects:

```
src/
├── BillFolder.Domain          POCO entities, enums, no dependencies
├── BillFolder.Application     Use cases, DTOs, validators, abstractions
│                              Depends only on Domain
├── BillFolder.Infrastructure  EF Core, Npgsql, JWT, Argon2 implementations
│                              Depends on Application + Domain
└── BillFolder.Api             Minimal API endpoints, DI wiring
                               Depends on Application + Infrastructure
```

`Application` defines `IApplicationDbContext`, `IPasswordHasher`, `IJwtTokenService`. `Infrastructure` implements them. `Api` consumes the interfaces via constructor injection.

---

## Local development

### Prerequisites

- .NET 10 SDK
- Docker Desktop
- `jq` (`brew install jq` — used in the smoke test scripts)

### Spin up Postgres + apply schema

```bash
docker compose -f docker-compose.dev.yml up -d
```

The `db/schema.sql` is mounted into the container's `/docker-entrypoint-initdb.d/` and runs on first boot. 18 tables, 6 enums, 18 seed categories.

### Run the API

```bash
cd src/BillFolder.Api
dotnet run --launch-profile https
```

API listens on `https://localhost:7285` (and `http://localhost:5077`).

### First request

```bash
curl -k https://localhost:7285/v1/health
# {"status":"ok","version":"0.1.1","timestamp":"..."}
```

### Smoke test the auth flow

```bash
API=https://localhost:7285

curl -k -X POST $API/v1/auth/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"strong-pass-123","displayName":"You"}'

# Login returns access + refresh tokens
curl -k -X POST $API/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"strong-pass-123"}'
```

### Restart workflow

The API does **not** auto-reload on code changes. After editing C#, stop and restart:

```bash
# In the dotnet run terminal: Ctrl+C
dotnet run --launch-profile https
```

If you want live reload, use `dotnet watch run` instead — but be aware that aggressive linting can make watch noisier than helpful.

---

## API surface

All endpoints (except `/v1/auth/*` and `/v1/health`) require `Authorization: Bearer <accessToken>`.

### Auth

| Method | Path | Notes |
| --- | --- | --- |
| POST | `/v1/auth/signup` | Email/password + displayName |
| POST | `/v1/auth/login` | Returns access + refresh tokens |
| POST | `/v1/auth/refresh` | Rotates refresh token (old one revoked) |
| POST | `/v1/auth/logout` | Revokes refresh token, idempotent |

### User & catalog

| Method | Path |
| --- | --- |
| GET | `/v1/users/me` |
| GET | `/v1/categories` |
| GET | `/v1/health` |

### Accounts

| Resource | Path | Operations |
| --- | --- | --- |
| Checking accounts | `/v1/checking-accounts` | full CRUD, `is_primary` rotation |
| Credit card accounts | `/v1/credit-card-accounts` | full CRUD |
| Savings accounts | `/v1/savings-accounts` | full CRUD, 1:1 with checking |

### Cycles

| Resource | Path | Operations |
| --- | --- | --- |
| Cycles | `/v1/cycles` | full CRUD + `/v1/cycles/current` |
| Cycle adjustments | `/v1/cycle-adjustments` | full CRUD (inflow / outflow) |

### Income (template + instance)

| Resource | Path |
| --- | --- |
| Income sources (template) | `/v1/income-sources` |
| Income entries (instance) | `/v1/income-entries` |

Entries have a computed `late` status: stored as `expected`, returned as `late` if `expectedDate < today`. Filter by `?status=late` returns both stored-late and computed-late.

### Expenses

| Resource | Path |
| --- | --- |
| Daily expenses | `/v1/daily-expenses` |
| Daily expense recurrences (template) | `/v1/daily-expense-recurrences` |
| Expenses | `/v1/expenses` |
| Expense recurrences (template) | `/v1/expense-recurrences` |

`/v1/expenses` has a computed `overdue` status (same pattern as income's `late`) and an auto-fill convenience: `PATCH` with `status=paid` automatically fills `paid_date=today` and `actual_amount=expected_amount` when not provided.

### Cards

| Resource | Path |
| --- | --- |
| Card entries (purchases) | `/v1/card-entries` |
| Card entry recurrences (template) | `/v1/card-entry-recurrences` |
| Card statements (faturas) | `/v1/card-statements` |

`POST /v1/card-entries` automatically:
1. Computes which statement period the purchase falls into (using the card's `closing_day` and `due_day`).
2. Distributes installments across consecutive statements.
3. Creates the statements that don't exist yet.
4. Persists the entry, statements, and installments atomically.

`/v1/card-statements` is read + status update only. Statements are system-managed via the card-entries flow.

### Savings

| Resource | Path |
| --- | --- |
| Savings transactions | `/v1/savings-transactions` |

Five types: `deposit`, `withdrawal`, `yield`, `transferIn`, `transferOut`. The `linkedTransactionId` field lets clients track the matching transaction in another savings account for transfers (no enforcement on this side).

### Dashboard

| Method | Path |
| --- | --- |
| GET | `/v1/home` |

Single endpoint composing the cycle snapshot for the home screen:

```json
{
  "cycle": { "id", "startDate", "endDate", "label" },
  "balance": {
    "checkingAccountsTotal", "expectedIncome", "receivedIncome",
    "expectedExpenses", "paidExpenses", "expectedCardStatements",
    "dailyExpensesSpent",
    "remaining": "expectedIncome - expectedExpenses - expectedCardStatements - dailyExpensesSpent"
  },
  "incomeBreakdown":  { "expected", "received", "late", "notOccurred" },
  "expenseBreakdown": { "pending", "overdue", "paid" },
  "upcomingExpenses": [ /* next 5 unpaid */ ],
  "cardStatementsInCycle": [ /* statements due this cycle */ ]
}
```

Returns 404 `no_cycle` if the user has no cycle covering today.

---

## Production deployment

### How a deploy happens

1. `git push origin main`
2. GitHub Actions builds the .NET solution, runs tests
3. Docker image built and pushed to GHCR (tagged `latest` and short SHA)
4. SSH to the VPS as user `deploy`
5. `docker compose pull api && docker compose up -d api`
6. Healthcheck loop polls `/v1/health` until 200
7. `sudo systemctl reload nginx` (passwordless via narrow sudoers rule)

End-to-end: about 3 minutes.

### Production stack on the VPS

```
nginx (TLS termination + reverse proxy)
  -> http://127.0.0.1:8080 (BillFolder.Api container)
       -> postgres:5432 (Postgres container, same Docker network)

Backups: pg_dump into /opt/billfolder/backups/, daily at 03:00 UTC,
         7-day retention, via cron + pg_dump | gzip
```

### Required environment variables on the VPS

In `/opt/billfolder/.env`:

```
POSTGRES_DB=billfolder
POSTGRES_USER=billfolder
POSTGRES_PASSWORD=<random>

Jwt__Key=<random hex 64+ chars>
Jwt__Issuer=billfolder
Jwt__Audience=billfolder-app
Jwt__AccessTokenMinutes=30
Jwt__RefreshTokenDays=14
```

### GitHub secrets

| Secret | Purpose |
| --- | --- |
| `VPS_HOST` | IP address of the VPS |
| `VPS_USER` | SSH user (`deploy`) |
| `VPS_SSH_KEY` | Private SSH key authorized in `/home/deploy/.ssh/authorized_keys` |

---

## Project structure

```
.
├── .editorconfig                 Style rules + lint severities
├── .github/workflows/            CI/CD: build & deploy
├── Directory.Build.props         Treat warnings as errors, analyzer level, etc.
├── Dockerfile                    Multi-stage build (SDK -> aspnet runtime)
├── docker-compose.dev.yml        Postgres for local dev, mounts schema.sql
├── BillFolder.slnx               Solution (XML format, .NET 10)
├── db/
│   ├── schema.sql                Source of truth for the schema
│   └── migrations/               Idempotent SQL scripts for prod sync
├── src/
│   ├── BillFolder.Api/
│   │   ├── Endpoints/            Minimal API per resource
│   │   ├── Extensions/           ClaimsPrincipal helpers
│   │   ├── Properties/           launchSettings.json (https profile on 7285)
│   │   └── Program.cs            DI wiring, JWT bearer setup, MapXxxEndpoints
│   ├── BillFolder.Application/
│   │   ├── Abstractions/         Auth + Persistence interfaces
│   │   ├── Common/               OperationResult<T>
│   │   ├── Dtos/                 Request/response records by feature
│   │   ├── UseCases/             Service classes by feature
│   │   └── Validators/           FluentValidation by feature
│   ├── BillFolder.Domain/
│   │   ├── Entities/             18 POCO entities mapped from schema
│   │   └── Enums/                6 enums mirroring Postgres ENUMs
│   └── BillFolder.Infrastructure/
│       ├── Auth/                 Argon2idPasswordHasher, JwtTokenService
│       ├── Persistence/
│       │   ├── ApplicationDbContext.cs  Implements IApplicationDbContext
│       │   ├── Configurations/   IEntityTypeConfiguration<T> per entity
│       │   └── Migrations/       EF Core migrations (baseline + AddRefreshTokens)
│       └── DependencyInjection.cs   AddInfrastructure() extension
└── tests/
    └── BillFolder.Api.Tests/     xUnit, Testcontainers (placeholder)
```

---

## Design decisions worth documenting

**UUID v7, generated client-side.**
The C# code calls `Guid.CreateVersion7()` and inserts with the value set. The schema's `gen_random_uuid()` default exists only as a safety net for ad-hoc inserts. v7 GUIDs are time-ordered, which is great for indexes and pagination.

**Templates as opt-in, instances as the source of truth.**
`expense_recurrences`, `daily_expense_recurrences`, and `card_entry_recurrences` are templates the user can manage and reference, but instances (`expenses`, `daily_expenses`, `card_entries`) are independently editable and are what the cycle aggregates run against. No automatic generation yet — that's a future feature.

**Postgres ENUMs require four registrations in EF Core 10.**
For an enum like `ExpenseStatus` mapped to the Postgres ENUM `expense_status`, you need:

1. `modelBuilder.HasPostgresEnum<ExpenseStatus>("public", "expense_status")` in `OnModelCreating`.
2. `dataSourceBuilder.MapEnum<ExpenseStatus>("expense_status")` on `NpgsqlDataSourceBuilder`.
3. `npgsql.MapEnum<ExpenseStatus>("expense_status")` inside `UseNpgsql(dataSource, npgsql => ...)`.
4. `.HasColumnType("expense_status")` on the entity property in its configuration.

Skip any of these and EF Core sends `int` to a column expecting the ENUM type, and Postgres rejects with "expression is of type integer". Discovered the hard way.

**Computed statuses, not stored.**
`overdue` (expenses) and `late` (income) aren't stored — they're derived at response time from the stored `pending`/`expected` plus the date comparison against today. Storing them would require a daily job to flip statuses; computing avoids the complexity. Status filters on list endpoints handle the same logic in the WHERE clause.

**No migrations on container startup.**
The API container does not run migrations on boot. Schema changes go through a manual idempotent SQL script applied to prod via SSH (see `db/migrations/`). Eventually this will move into the CI/CD pipeline, but for an MVP it's safer to keep schema changes deliberate.

**JsonStringEnumConverter is JSON only — query string binding is separate.**
ASP.NET Core's minimal API doesn't apply the JSON enum converter to query parameters. Endpoints that filter by enum receive `string?` and parse manually with `Enum.TryParse(..., ignoreCase: true)`, returning a 400 with a readable error if the value isn't recognized.

**OperationResult instead of exceptions for control flow.**
Use cases return `OperationResult<T>` with `IsSuccess`, `Value`, `ErrorCode`, `ErrorMessage`. Endpoints map `ErrorCode` to HTTP status (validation_error -> 400, not_found -> 404, etc.). Exceptions stay for genuinely unexpected failures.

---

## Roadmap

### Implemented

- 18 aggregates with full CRUD
- JWT auth with refresh token rotation
- Card flow with auto-distribution of installments across statements
- `/v1/home` dashboard composing the cycle snapshot
- CI/CD with auto-deploy to production

### Not yet implemented (none of these block the mobile client)

- Auto-generation of cycles from the 5th-business-day rule
- Auto-creation of expense / daily-expense / card-entry instances from recurrence templates
- Linking expense status `paid` back to card statement status `paid` (the schema has `linked_expense_id` and `linked_card_statement_id`; the wiring is manual for now)
- Email verification on signup
- Password reset flow
- Soft delete + audit trail
- Rate limiting on `/v1/auth/*`
- OpenTelemetry traces

### Mobile client

A native Android app (Kotlin + Jetpack Compose + Hilt + Retrofit) that consumes this API. The design system is documented in the project notes: dollar-bill green seed (`#86BC65`), Barlow Semi Condensed for body, "Byker" for the wordmark.

---

## License

Private. All rights reserved.
