# BillFolder Web MVP Final Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Do not dispatch subagents for this plan.

**Goal:** Resolve all eight Important and four Minor findings in the binding whole-product review, with scoped backend/web commits and complete local verification.

**Architecture:** The backend gains a refresh-token family, token-issued expiry metadata, and a single business-civil-date provider backed by `TimeProvider` and a configured IANA zone. The web reuses existing payment dialogs through stable route query parameters, coordinates cookie mutations across tabs, refreshes only the active route after five minutes, and retains stale content with explicit refresh diagnostics. Preview and security contracts remain exact-origin and production-strength.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/Npgsql, PostgreSQL idempotent SQL, Angular 22 standalone components/signals, Angular Material/CDK, Vitest, Playwright, Node 24.15.0.

**Spec:** `docs/superpowers/specs/2026-10-07-billfolder-web-mvp-design.md`

## Global Constraints

- Binding findings: `.superpowers/sdd/2026-10-07-billfolder-web-mvp/final-whole-review.md`.
- One shared resume threshold: five minutes; no continuous polling.
- Authenticated preview is exactly `https://preview.billfolder.app` calling `https://api.billfolder.app`.
- Business zone defaults to `America/Sao_Paulo`; every domain `Today` comes from one injected abstraction.
- Refresh-family schema uses hand-written idempotent SQL and exact EF mapping; do not generate an EF migration or rewrite the known snapshot drift.
- Every successful write publishes exactly one invalidation and failed writes publish none.
- Keep Node 24.15.0, shell-only PWA caching, strict cookie/CSP/CORS flags, and no push/deploy/remote/Cloudflare/SSH/production action.
- Work directly on the already-authorized local `main` branches and leave both repositories clean.

## Review Focus

- A predecessor refresh cookie must still revoke its active successor family on logout; integration test the rotated-predecessor path.
- São Paulo dates on both sides of local midnight must classify and default dates correctly across all consumers.
- Deep links must select the exact cycle/expense or card/statement and open only one existing payment dialog.
- A stale-content refresh failure must remain visible, retryable, and bounded to the active route.
- Rapid destructive activation and dialog dismissal during writes must not duplicate or orphan mutations.

---

### Task 1: Backend session family, exact expiry, and preview origin

**Files:**
- Modify: `src/BillFolder.Domain/Entities/RefreshToken.cs`
- Modify: `src/BillFolder.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`
- Modify: `src/BillFolder.Application/Abstractions/Auth/IJwtTokenService.cs`
- Modify: `src/BillFolder.Infrastructure/Auth/JwtTokenService.cs`
- Modify: `src/BillFolder.Application/UseCases/Auth/AuthService.cs`
- Modify: `src/BillFolder.Api/appsettings.json`
- Create: `db/migrations/20261008120000_AddRefreshTokenFamilies.sql`
- Test: `tests/BillFolder.Api.Tests/Auth/WebAuthEndpointsTests.cs`
- Test: `tests/BillFolder.Api.Tests/Infrastructure/ApiWebApplicationFactory.cs`

**Interfaces:**
- Produces: `AccessTokenResult(Token, ExpiresAt)` and `RefreshToken.FamilyId`.
- Consumes: existing cookie endpoints and Android JSON auth contracts unchanged.

- [ ] Add integration tests proving predecessor logout revokes the rotated successor, preview CORS accepts only the exact origin, Android responses remain compatible, and `accessTokenExpiresAt` follows a non-30-minute JWT configuration.
- [ ] Run the focused auth tests and record the expected RED failures.
- [ ] Implement family propagation/revocation, token-issued expiry, exact preview origin, SQL migration, EF mapping, and test-database application of the scoped SQL.
- [ ] Run focused auth/migration tests GREEN, then the backend suite.
- [ ] Commit the coherent backend auth/schema change.

### Task 2: Backend business date and independent Home cohorts

**Files:**
- Create: `src/BillFolder.Application/Abstractions/Time/IBusinessDateProvider.cs`
- Create: `src/BillFolder.Infrastructure/Time/BusinessDateProvider.cs`
- Create: `src/BillFolder.Infrastructure/Time/BusinessTimeOptions.cs`
- Modify: all Application services/expansion helpers found by the exhaustive UTC-to-`DateOnly` search.
- Modify: `src/BillFolder.Application/Dtos/Home/HomeDtos.cs`
- Modify: `src/BillFolder.Application/UseCases/Home/HomeService.cs`
- Test: domain/integration tests under `tests/BillFolder.Api.Tests` for Home, expenses, income, cards, cycles, and recurrence expansion.

**Interfaces:**
- Produces: `IBusinessDateProvider.Today` and separate bounded `OverdueExpenses`/`UpcomingExpenses` response cohorts.
- Consumes: injected `TimeProvider` plus `BusinessTime:TimeZoneId`, default `America/Sao_Paulo`.

- [ ] Add boundary tests at `02:59:59Z` and `03:00:00Z` and a Home fixture with six overdue plus two upcoming expenses.
- [ ] Run focused tests and record RED failures.
- [ ] Implement the shared provider, inject it into every discovered consumer, pass civil today into static expansion helpers, and split Home cohorts before each limit.
- [ ] Run focused tests GREEN, repeat the exhaustive search, then run the backend suite and clean build.
- [ ] Commit the backend civil-date/Home contract change.

### Task 3: Cross-tab browser authentication

**Files:**
- Create: `BillFolderWeb/src/app/core/auth/auth-coordination.service.ts`
- Modify: `BillFolderWeb/src/app/core/auth/auth-session.service.ts`
- Modify: `BillFolderWeb/src/app/core/layout/app-shell.component.ts`
- Test: matching unit specs and `BillFolderWeb/e2e/auth.spec.ts`/fixture.

**Interfaces:**
- Produces: Web-Lock-backed cookie mutation exclusivity, BroadcastChannel logout propagation, storage-event fallback, and lifecycle cleanup.
- Consumes: current in-memory session and cookie APIs; no token persistence.

- [ ] Add unit tests for lock/fallback serialization, remote logout clearing, and cleanup plus a two-page same-context race E2E.
- [ ] Run focused tests/E2E and record RED failures.
- [ ] Implement coordination around existing mutation queue and route other tabs to login on broadcast clear.
- [ ] Run focused tests/E2E GREEN and commit.

### Task 4: Home settlement actions and stable deep links

**Files:**
- Modify: Home projection-list/page files and specs.
- Modify: Expenses and Cards pages/specs for one-shot query-param actions.
- Modify: E2E API fixture and payment specs.

**Interfaces:**
- Produces: `/despesas?cycleId=...&expenseId=...&action=pay|pay-occurrence` and `/cartoes?cardId=...&statementId=...&action=pay-statement`.
- Consumes: existing `PayExpenseDialogComponent`, `PayOccurrenceDialogComponent`, and `PayStatementDialogComponent` only.

- [ ] Add component and E2E tests for all three action kinds and no control for `none`.
- [ ] Run RED, implement accessible links and one-shot exact-target selection/dialog opening, run GREEN.
- [ ] Commit the Home/deep-link change.

### Task 5: Active-route freshness and non-destructive read errors

**Files:**
- Create: active-route refresh registry/coordinator and shared refresh-status component.
- Modify: `LoadState` plus cycle/home/expenses/daily/income/cards/savings/adjustments stores and pages.
- Test: coordinator, stores, Home partial request, and shell lifecycle specs.

**Interfaces:**
- Produces: shared `TAB_RESUME_STALE_MS = 300_000`, one active callback, one in-flight refresh, `refreshError`, and `lastSuccessfulAt`.
- Consumes: `visibilitychange` and `pageshow`, with listener cleanup and no timer/polling.

- [ ] Add tests for stale/fresh resume, cleanup, bounded fan-out, manual failure, invalidation failure, and Home recent loading/content/error.
- [ ] Run RED; implement metadata preservation, retry UI, and active-route registration.
- [ ] Run GREEN and commit.

### Task 6: Write locking and scoped destructive pending state

**Files:**
- Modify: expense form component/template/spec.
- Modify: cards store/page/installment table and specs.

**Interfaces:**
- Produces: `WriteDialogLock` usage for expense saves and pending delete keys `${cardId}:${statementId}:${entryId}`.
- Consumes: existing write/invalidation and focus-restoration behavior.

- [ ] Add delayed success/failure dialog tests and duplicate/stale-scope delete tests; run RED.
- [ ] Implement lock/disabled dismiss controls and guarded scoped pending deletion; run GREEN.
- [ ] Commit.

### Task 7: Preview, cookie/SCA contracts, and truthful readiness

**Files:**
- Modify: web environments, `angular.json`, `package.json`, static/artifact tests, CSP headers, CI workflow, README.
- Modify: backend/web Task 14/handoff documentation and create the requested final-fix report.

**Interfaces:**
- Produces: `npm run build:preview`, exact preview API/CSP/origin contract, `npm audit --audit-level=high`, and documented exception process.

- [ ] Add static/artifact tests for preview, Strict production cookie contract, no wildcard/pages.dev authenticated preview, and audit policy; run RED.
- [ ] Implement configuration and docs; run GREEN and preview/production builds.
- [ ] Commit web config/docs and backend report/docs separately; update the external handoff outside Git.

### Task 8: Final verification and repository hygiene

- [ ] Run targeted checks after every change and capture RED/GREEN evidence in `final-fix-report.md`.
- [ ] Run backend `dotnet test BillFolder.slnx` and `dotnet build BillFolder.slnx --no-incremental`.
- [ ] Select Node 24.15.0, run web `npm ci`, full unit/static, production and preview builds/artifact checks, full E2E, and responsive/accessibility smoke.
- [ ] Run Prettier and inspect both diffs, migration scope, logs, SHAs, and clean worktrees.
- [ ] Mark readiness as `awaiting scoped re-review`; do not claim production readiness.
