# BillFolder Web MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar um Angular PWA responsivo em `app.billfolder.app` que cubra a rotina financeira atual da Jess usando a API existente, com autenticação web segura e fundação pronta para a paridade com o Android.

**Architecture:** Um novo repositório `BillFolderWeb` conterá uma SPA Angular 22 standalone, organizada em `core`, `shared` e features lazy-loaded. A API .NET 10 continuará como fonte de verdade e ganhará somente autenticação web por cookie e CORS restrito; os contratos Android permanecerão compatíveis. Cada feature terá API client, store com Signals e componentes próprios, conectados por um `DataChangeService` global para invalidação após writes.

**Tech Stack:** Angular 22, TypeScript 6, Angular Material/CDK 22, SCSS, Signals, RxJS 7, Vitest, Playwright, PWA service worker, .NET 10, xUnit, Testcontainers PostgreSQL, Cloudflare Pages.

**Spec:** `docs/superpowers/specs/2026-10-07-billfolder-web-mvp-design.md`

## Global Constraints

- Criar o frontend em `/Users/jesstoselli/Repos/BillFolderWeb`, repositório separado de backend e Android.
- Usar Angular `^22.0.0`, Node `>=24.15.0`, npm e `package-lock.json` versionado.
- Usar componentes standalone, rotas lazy-loaded, SCSS e client-side rendering; não adicionar SSR nem NgRx.
- Manter dark-first, Barlow Semi Condensed e `#86BC65`; Angular Material/CDK fornece primitivas, não aparência genérica.
- PWA instalável, mas dados online-only: não cachear respostas da API nem enfileirar writes offline.
- Nunca guardar access token ou refresh token em `localStorage`, `sessionStorage` ou IndexedDB.
- Preservar integralmente os endpoints móveis de autenticação e os contratos já usados pelo Android.
- Tratar `YYYY-MM-DD` como data civil; não passar por `new Date(iso)` para exibição.
- Todo write bem-sucedido deve publicar uma mudança global; write falho não publica.
- Nenhum secret no bundle web; somente URLs e flags públicas em ambientes.
- Não criar repositório remoto, configurar Cloudflare nem publicar produção sem autorização explícita da usuária.

## Review Focus

- Duas chamadas simultâneas que recebem `401` devem compartilhar exatamente um refresh e repetir ambas apenas uma vez — teste em Task 4.
- `YYYY-MM-DD` deve produzir a mesma data em qualquer timezone, inclusive UTC-03 e UTC+14 — teste em Task 5.
- Write HTTP falho não pode incrementar o `DataChangeService` nem apagar o formulário — teste em Task 5 e nos stores de feature.
- Despesa provisionada parcialmente paga deve mostrar o restante, nunca valor negativo, preservando “R$ X no mês” — teste em Task 7.
- Fatura `open` não pode oferecer pagamento; `closed` oferece e `paid` mostra confirmação sem repetir write — teste em Task 11.

---

### Task 1: Scaffold do `BillFolderWeb` e pipeline local

**Files:**
- Create via Angular CLI: `/Users/jesstoselli/Repos/BillFolderWeb/angular.json`
- Create via Angular CLI: `/Users/jesstoselli/Repos/BillFolderWeb/package.json`
- Create via Angular CLI: `/Users/jesstoselli/Repos/BillFolderWeb/package-lock.json`
- Create via Angular CLI: `/Users/jesstoselli/Repos/BillFolderWeb/tsconfig.json`
- Create via Angular CLI: `/Users/jesstoselli/Repos/BillFolderWeb/src/main.ts`
- Create via Angular CLI: `/Users/jesstoselli/Repos/BillFolderWeb/src/index.html`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/environments/environment.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/environments/environment.development.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/config/app-environment.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/.github/workflows/ci.yml`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/angular.json`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/package.json`
- Test: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.spec.ts`

**Interfaces:**
- Produces: `APP_ENVIRONMENT: InjectionToken<AppEnvironment>` with `{ apiBaseUrl: string; production: boolean }`.
- Produces: scripts `test:ci`, `build:prod`, and CI that runs test then build.

- [ ] **Step 1: Verify toolchain and scaffold the repository**

Run from `/Users/jesstoselli/Repos`:

```bash
node --version
npm --version
npx @angular/cli@22 new BillFolderWeb --routing --style=scss --standalone --ssr=false --package-manager=npm --skip-git=false --defaults
cd BillFolderWeb
npx ng add @angular/material@22 --theme=custom --typography=false --animations=enabled --skip-confirmation
```

Expected: Node is `>=24.15.0`; Angular creates a standalone app and Material/CDK dependencies are present.

- [ ] **Step 2: Replace the generated smoke test with the application contract**

```typescript
it('renders only the routing host at the root', async () => {
  const fixture = TestBed.createComponent(App);
  await fixture.whenStable();
  expect(fixture.nativeElement.querySelector('router-outlet')).not.toBeNull();
  expect(fixture.nativeElement.textContent.trim()).toBe('');
});
```

Run: `npx ng test --watch=false`
Expected: FAIL until the generated placeholder template is replaced.

- [ ] **Step 3: Add environment injection and minimal root**

```typescript
export interface AppEnvironment {
  apiBaseUrl: string;
  production: boolean;
}

export const APP_ENVIRONMENT = new InjectionToken<AppEnvironment>('APP_ENVIRONMENT');
```

Set development to `http://localhost:5077/v1` and production to `https://api.billfolder.app/v1`. Register the token in `app.config.ts`, using the replaced environment file selected by `angular.json`. Root template contains only `<router-outlet />` plus an accessible skip link.

- [ ] **Step 4: Add reproducible scripts and CI**

```json
{
  "scripts": {
    "test:ci": "ng test --watch=false",
    "build:prod": "ng build --configuration production"
  }
}
```

```yaml
name: CI
on:
  pull_request:
  push:
    branches: [main]
jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 24.15.0
          cache: npm
      - run: npm ci
      - run: npm run test:ci
      - run: npm run build:prod
```

- [ ] **Step 5: Verify and commit**

Run: `npm run test:ci && npm run build:prod`
Expected: all tests pass and `dist/bill-folder-web/browser` exists.

```bash
git add .
git commit -m "chore: scaffold BillFolder Web on Angular 22"
```

---

### Task 2: Design system e shell responsivo “Painel focado”

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/styles/_tokens.scss`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/styles/_material-theme.scss`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/styles/_typography.scss`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/styles.scss`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/layout/app-shell.component.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/layout/sidebar.component.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/layout/shell.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/components/cycle-navigator/cycle-navigator.component.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/components/money/money.component.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/components/page-state/page-state.component.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/components/feature-placeholder/feature-placeholder.component.ts`
- Test: matching `*.spec.ts` files beside the components/stores

**Interfaces:**
- Produces: `ShellStore` with `mode: Signal<'full'|'rail'|'drawer'>`, `drawerOpen`, `openDrawer()`, `closeDrawer()`.
- Produces: `CycleNavigatorComponent` inputs `label`, `startDate`, `endDate`, `previousEnabled`, `nextEnabled`; outputs `previous`, `next`.
- Produces: CSS tokens `--bf-*` and utility classes only for typography/layout, not feature behavior.

- [ ] **Step 1: Write shell and navigation tests**

```typescript
it('renders every MVP destination with Home active', () => {
  fixture.componentRef.setInput('activeRoute', '/home');
  fixture.detectChanges();
  const links = [...fixture.nativeElement.querySelectorAll('a')];
  expect(links.map(link => link.textContent.trim())).toEqual([
    'Home', 'Despesas', 'Gastos diários', 'Receitas', 'Cartões', 'Poupança', 'Ajustes'
  ]);
  expect(fixture.nativeElement.querySelector('[aria-current="page"]')?.textContent).toContain('Home');
});

it('closes drawer after navigation on narrow layout', () => {
  store.setMode('drawer');
  store.openDrawer();
  store.navigationCompleted();
  expect(store.drawerOpen()).toBe(false);
});
```

Run: `npm run test:ci`
Expected: FAIL because shell files do not exist.

- [ ] **Step 2: Implement BillFolder tokens and Material theme**

Run: `npm install @fontsource/barlow-semi-condensed`

Define exact core tokens:

```scss
:root {
  color-scheme: dark;
  --bf-brand: #86bc65;
  --bf-bg: #0a0d0a;
  --bf-surface: #1e1e1e;
  --bf-surface-high: #2d2d2d;
  --bf-text: #e1e3db;
  --bf-muted: #c2c9bb;
  --bf-outline: #42493d;
  --bf-primary: #bbe89f;
  --bf-danger: #e09999;
  --bf-radius-card: 16px;
  --bf-radius-hero: 28px;
}
```

Import `@fontsource/barlow-semi-condensed/400.css` and `500.css`; the package keeps the WOFF2 assets in the built application. Map Angular Material theme colors to the tokens.

- [ ] **Step 3: Implement responsive shell and shared primitives**

Use `BreakpointObserver` only inside `ShellStore`: full at `>=1200px`, rail at `768–1199px`, drawer below `768px`. Sidebar links use routerLink and visible labels in full mode; rail links retain `aria-label` and tooltips. `AppShellComponent` renders `<router-outlet>` in the content area.

- [ ] **Step 4: Wire lazy route placeholders**

```typescript
export const routes: Routes = [
  {
    path: '',
    component: AppShellComponent,
    children: [
      { path: 'home', component: FeaturePlaceholderComponent, data: { title: 'Home' } },
      { path: '', pathMatch: 'full', redirectTo: 'home' },
    ],
  },
  { path: '**', redirectTo: 'home' },
];
```

Until each feature task replaces it, route the remaining destinations to `FeaturePlaceholderComponent` with a real title and no invented actions.

- [ ] **Step 5: Verify accessibility/build and commit**

Run: `npm run test:ci && npm run build:prod`
Expected: shell tests pass; no template compile errors.

```bash
git add src
git commit -m "feat(ui): add focused dashboard shell and design system"
```

---

### Task 3: Backend web auth, cookie e CORS

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Infrastructure/Auth/WebAuthOptions.cs`
- Create: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Api/Endpoints/WebAuthEndpoints.cs`
- Create: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Api/Auth/WebAuthCookie.cs`
- Create: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Api/Auth/WebOriginFilter.cs`
- Modify: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Application/Dtos/Auth/AuthDtos.cs`
- Modify: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Api/Program.cs`
- Modify: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Api/appsettings.json`
- Modify: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Api/appsettings.Development.json`
- Create: `/Users/jesstoselli/Repos/BillFolder/tests/BillFolder.Api.Tests/Infrastructure/ApiWebApplicationFactory.cs`
- Create: `/Users/jesstoselli/Repos/BillFolder/tests/BillFolder.Api.Tests/Auth/WebAuthEndpointsTests.cs`

**Interfaces:**
- Produces: `WebAuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, UserDto User)`.
- Produces: browser endpoints `/v1/auth/web/{signup,login,refresh,logout}`.
- Produces: cookie name `bf_refresh`, configured allowed origins, unchanged mobile endpoints.

- [ ] **Step 1: Write failing integration tests**

The factory starts `PostgreSqlContainer`, injects its connection string, runs EF migrations and supplies deterministic JWT settings. Add tests:

```csharp
[Fact]
public async Task WebLogin_SetsSecureHttpOnlyCookie_WithoutReturningRefreshToken()
{
    var response = await Client.PostAsJsonAsync("/v1/auth/web/login", new { email, password });
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadAsStringAsync();
    json.Should().NotContain("refreshToken");
    response.Headers.GetValues("Set-Cookie").Single().Should()
        .ContainAll("bf_refresh=", "httponly", "secure", "samesite=strict");
}

[Fact]
public async Task WebRefresh_RotatesCookie_AndRejectsReusingTheOldCookie()
{
    var login = await LoginWebAsync();
    var oldCookie = RefreshCookieFrom(login);
    var firstRefresh = await RefreshWebAsync(oldCookie);
    firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
    RefreshCookieFrom(firstRefresh).Should().NotBe(oldCookie);
    var replay = await RefreshWebAsync(oldCookie);
    replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task MobileLogin_StillReturnsRefreshToken()
{
    var response = await Client.PostAsJsonAsync("/v1/auth/login", new { email, password });
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    json.TryGetProperty("refreshToken", out var token).Should().BeTrue();
    token.GetString().Should().NotBeNullOrWhiteSpace();
}

[Fact]
public async Task Cors_DoesNotAllowUnknownOrigin()
{
    using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/web/refresh");
    request.Headers.Add("Origin", "https://evil.example");
    request.Headers.Add("Access-Control-Request-Method", "POST");
    var response = await Client.SendAsync(request);
    response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
}
```

Define `LoginWebAsync`, `RefreshWebAsync` and `RefreshCookieFrom` as private helpers in the test class; each sends a real HTTP request through the factory and extracts only the `bf_refresh` name/value pair from `Set-Cookie`.

Run: `dotnet test --filter FullyQualifiedName~WebAuthEndpointsTests`
Expected: FAIL because web endpoints/options do not exist.

- [ ] **Step 2: Add web response and cookie policy**

```csharp
public sealed record WebAuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    UserDto User);

public sealed class WebAuthOptions
{
    public const string SectionName = "WebAuth";
    public string CookieName { get; init; } = "bf_refresh";
    public string[] AllowedOrigins { get; init; } = [];
}
```

`WebAuthCookie.Append` sets `HttpOnly=true`, `Secure=!environment.IsDevelopment()`, `SameSite=Strict`, `Path=/v1/auth/web`, and `Expires=response.RefreshTokenExpiresAt`. `Delete` uses identical path and flags.

- [ ] **Step 3: Implement web endpoints by delegating to `AuthService`**

Login/signup call existing service methods, move the returned refresh token to the cookie and map the remaining fields to `WebAuthResponse`. Refresh reads the cookie, calls `RefreshAsync`, rotates it and returns the mapped response. Logout revokes the cookie token through `LogoutAsync`, deletes the cookie and remains idempotent.

Invalid or absent refresh cookie returns the existing `invalid_refresh_token` shape with status 401. Attach `WebOriginFilter` to the four web-auth routes; it accepts a missing `Origin` for non-browser test/CLI clients, accepts an exact configured origin, and returns 403 for any other supplied origin.

- [ ] **Step 4: Configure strict CORS and middleware order**

```csharp
builder.Services.AddCors(options => options.AddPolicy("BillFolderWeb", policy =>
    policy.WithOrigins(webAuthOptions.AllowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

app.UseCors("BillFolderWeb");
app.UseAuthentication();
app.UseAuthorization();
```

Production configuration contains only `https://app.billfolder.app`; development contains `http://localhost:4200`. Map `WebAuthEndpoints` without changing `AuthEndpoints`.

- [ ] **Step 5: Run focused and full backend verification**

Run:

```bash
dotnet test --filter FullyQualifiedName~WebAuthEndpointsTests
dotnet test
dotnet build --no-incremental
```

Expected: web tests and existing mobile tests pass; 0 warnings and 0 errors.

- [ ] **Step 6: Commit backend change**

```bash
git add src tests
git commit -m "feat(auth): add secure browser session endpoints"
```

---

### Task 4: Frontend auth completo e refresh concorrente

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/auth/auth.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/auth/auth.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/auth/auth-session.service.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/auth/auth.interceptor.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/auth/auth.guard.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/auth/login/login.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/auth/signup/signup.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/auth/forgot-password/forgot-password.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/auth/reset-password/reset-password.page.{ts,html,scss}`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.config.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.routes.ts`
- Test: specs beside API, session, interceptor, guard and each form

**Interfaces:**
- Produces: `AuthSessionService.user`, `isAuthenticated`, `restore()`, `login()`, `signup()`, `logout()`, `refreshOnce()`.
- Produces: functional `authInterceptor` that attaches Bearer and coordinates one retry.

- [ ] **Step 1: Write session and interceptor tests first**

```typescript
it('keeps tokens in memory and restores through cookie refresh', async () => {
  await firstValueFrom(session.restore());
  expect(session.isAuthenticated()).toBe(true);
  expect(localStorage.length).toBe(0);
  expect(sessionStorage.length).toBe(0);
});

it('shares one refresh across concurrent 401 responses', async () => {
  const a = firstValueFrom(http.get('/v1/a'));
  const b = firstValueFrom(http.get('/v1/b'));
  backend.expectOne('/v1/a').flush(null, { status: 401, statusText: 'Unauthorized' });
  backend.expectOne('/v1/b').flush(null, { status: 401, statusText: 'Unauthorized' });
  backend.expectOne('/v1/auth/web/refresh').flush(authResponse);
  backend.expectOne('/v1/a').flush({ ok: true });
  backend.expectOne('/v1/b').flush({ ok: true });
  await Promise.all([a, b]);
});
```

Run: `npm run test:ci`
Expected: FAIL because auth infrastructure does not exist.

- [ ] **Step 2: Implement models, API and in-memory session**

```typescript
export type AuthState =
  | { kind: 'restoring' }
  | { kind: 'anonymous' }
  | { kind: 'authenticated'; user: UserDto; accessToken: string; expiresAt: string };
```

All `/auth/web/*` calls use `{ withCredentials: true }`. `refreshOnce()` stores an in-flight Observable with `shareReplay({ bufferSize: 1, refCount: false })` and clears it in `finalize`.

- [ ] **Step 3: Implement interceptor, initializer and guards**

Skip Bearer injection for public auth URLs. Retry a request at most once by setting an `HttpContextToken`; a second 401 clears the session. Register `provideAppInitializer(() => firstValueFrom(inject(AuthSessionService).restore()))`.

- [ ] **Step 4: Implement four auth flows with Reactive Forms**

Validation rules mirror backend validators. Login routes to `/home`; signup also establishes the session. Forgot-password always shows the same success message. Reset receives email from query param, accepts six-digit code and new password, then returns to login.

- [ ] **Step 5: Run tests/build and commit**

Run: `npm run test:ci && npm run build:prod`
Expected: auth tests pass, including one-refresh concurrency test.

```bash
git add src
git commit -m "feat(auth): add secure web session and account flows"
```

---

### Task 5: Infraestrutura de dados, ciclos e referências

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/http/api-error.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/http/api-url.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/data-change/data-change.service.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/data-change/notifying-write.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/cycles/cycle.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/cycles/cycles.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/cycles/cycle.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/reference/reference-data.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/states/load-state.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/formatters/{civil-date,money}.ts`
- Test: matching specs

**Interfaces:**
- Produces: `LoadState<T>`, `DataChangeService.version`, `notifyingWrite`, `CycleStore.current/cycles/previous/next`.
- Produces: `formatCivilDate`, `formatBrl`, categories/checking/card/savings reference reads.

- [ ] **Step 1: Write deterministic helper and invalidation tests**

```typescript
it('formats a civil date without converting it to an instant', () => {
  expect(parseCivilDate('2026-10-07')).toEqual({ year: 2026, month: 10, day: 7 });
  expect(formatCivilDate('2026-10-07')).toBe('07/10/2026');
});

it('bumps only after a successful write', async () => {
  const before = changes.version();
  await expect(firstValueFrom(notifyingWrite(changes, throwError(() => new Error('boom'))))).rejects.toThrow();
  expect(changes.version()).toBe(before);
});
```

Run: `npm run test:ci`
Expected: FAIL with missing helpers.

Run this spec once with `TZ=America/Sao_Paulo` and once with `TZ=Pacific/Kiritimati`; both outputs must be identical.

- [ ] **Step 2: Implement civil date and money formatters**

Split `YYYY-MM-DD` into numeric year/month/day and format from those values; never construct a `Date` from the string. Use one module-level `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })` for money.

- [ ] **Step 3: Implement data-change and load state**

```typescript
export type LoadState<T> =
  | { kind: 'loading' }
  | { kind: 'content'; data: T; refreshing: boolean }
  | { kind: 'error'; message: string };

@Injectable({ providedIn: 'root' })
export class DataChangeService {
  private readonly value = signal(0);
  readonly version = this.value.asReadonly();
  notify(): void { this.value.update(v => v + 1); }
}
```

`notifyingWrite` applies `tap({ next: () => changes.notify() })`; errors pass through untouched.

- [ ] **Step 4: Implement cycles and reference clients**

Port exact field names from `CycleResponse`, `CategoryDto`, `CheckingAccountResponse`, `CreditCardAccountResponse` and `SavingsAccountResponse`. `CycleStore` loads list + current, sorts by `startDate`, and computes bounded previous/next IDs without `LocalDate.now()` assumptions in tests.

- [ ] **Step 5: Verify and commit**

Run: `npm run test:ci && npm run build:prod`

```bash
git add src
git commit -m "feat(data): add shared cycles, references and invalidation"
```

---

### Task 6: Home “Painel focado”

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/home.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/home.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/home.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/home-projections.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/home.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/components/balance-hero/balance-hero.component.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/components/category-breakdown/category-breakdown.component.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/components/home-tabs/home-tabs.component.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/home/components/projection-list/projection-list.component.{ts,html,scss}`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.routes.ts`
- Test: home API/store/projections/components specs

**Interfaces:**
- Consumes: `CycleStore`, `DataChangeService`, shared formatters.
- Produces: `HomeStore.load(cycleId?)`, `refresh()`, `state`, `selectCycle()`.

- [ ] **Step 1: Write projection and store tests**

```typescript
it('shows remaining provisioned amount and original monthly amount', () => {
  const row = projectUpcoming({ expectedAmount: 800, paidToDate: 400, occurrencesTotal: 4, occurrencesPaid: 2 });
  expect(row.amount).toBe(400);
  expect(row.context).toBe('R$ 800,00 no mês');
});

it('merges expenses and statements by due date and separates overdue', () => {
  const result = collectHomeRows(
    [expense({ id: 'late', dueDate: '2026-10-02', status: 'overdue' }), expense({ id: 'next', dueDate: '2026-10-12', status: 'pending' })],
    [statement({ id: 'card', dueDate: '2026-10-10', status: 'closed' })],
  );
  expect(result.overdue.map(x => x.id)).toEqual(['late']);
  expect(result.upcoming.map(x => x.id)).toEqual(['card', 'next']);
});

it('refreshes in place after DataChangeService changes', () => {
  store.load('cycle-1');
  api.resolveHome(homeFixture);
  changes.notify();
  expect(store.state()).toEqual({ kind: 'content', data: homeFixture, refreshing: true });
});
```

Provide typed `expense`, `statement` and `homeFixture` factories in `home.fixtures.ts`; all dates are fixed in October 2026.

Run: `npm run test:ci`
Expected: FAIL because Home API/store/projections do not exist.

- [ ] **Step 2: Port Home contracts and implement API/store**

`HomeApi.get(cycleId?)` calls `/home/`. `HomeStore` also requests daily expenses in the selected cycle to build `últimas`, sorts descending and limits the visual list to the same count chosen for the Android Home. A change-version effect refreshes content without returning to loading.

- [ ] **Step 3: Implement projections and action metadata**

Projection type must identify `expense`, `provisionedExpense` or `cardStatement`, carry status, due date and permitted action. Clamp `expectedAmount - paidToDate` at zero.

- [ ] **Step 4: Build focused dashboard layout**

Desktop uses dominant left column for hero + tabs, right column for breakdown + shortcuts; under 1200px it becomes one column. Tabs are keyboard-operable pills and overdue count is a badge. Hatching appears only in hero.

- [ ] **Step 5: Verify and commit**

Run: `npm run test:ci && npm run build:prod`

```bash
git add src/app/features/home src/app/app.routes.ts
git commit -m "feat(home): add focused financial dashboard"
```

---

### Task 7: Despesas — leitura, CRUD e projeções

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/expenses.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/expenses.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/expenses.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/expense-projections.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/expenses.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/components/expense-form/*`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.routes.ts`
- Test: API/store/projection/form specs

**Interfaces:**
- Produces: `ExpensesStore.load(cycle)`, `create`, `update`, `deleteOne`, grouped projections.
- Produces: `ExpenseFormValue` mapping to `CreateExpenseRequest`/`UpdateExpenseRequest`.

- [ ] **Step 1: Write store/projection/form tests**

Cover pending/overdue/paid grouping, optimistic delete rollback on HTTP failure, retained form values after failed save, and provisioned calculation:

```typescript
it('never shows a negative provisioned remainder', () => {
  expect(projectExpense({ expectedAmount: 800, paidToDate: 900, occurrencesTotal: 4 }).displayAmount).toBe(0);
});
```

Run: `npm run test:ci`
Expected: FAIL with missing expenses feature.

- [ ] **Step 2: Port exact expense contracts and implement client**

Implement GET by cycle dates, POST, PATCH and DELETE `?scope=`. Include `templateId`, `occurrenceAmount`, `occurrencesTotal`, `occurrencesPaid` and `paidToDate`; map response strings without inventing frontend status enums unknown to the API.

- [ ] **Step 3: Implement store and grouped page**

Store reacts to selected cycle and data-change versions. Page renders `atrasadas`, `próximas` and `pagas` using one accessible table/list component; explicit row menu replaces swipe.

- [ ] **Step 4: Implement create/edit dialog or side panel**

Short one-off create uses dialog. Edit form opens side panel, loads categories, validates label/amount/category/date and keeps server errors visible. Provisioned entries do not enter generic total editing; they route to Task 8 reprice.

- [ ] **Step 5: Verify and commit**

Run: `npm run test:ci && npm run build:prod`

```bash
git add src/app/features/expenses src/app/app.routes.ts
git commit -m "feat(expenses): add expense list and one-off CRUD"
```

---

### Task 8: Despesas — pagamento e recorrências

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/components/pay-expense-dialog/*`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/components/pay-occurrence-dialog/*`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/components/recurrence-form/*`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/expenses/components/reprice-provisioned-dialog/*`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/dialogs/recurrence-scope-dialog/*`
- Modify: expenses models/API/store/page from Task 7
- Test: dialog and store specs

**Interfaces:**
- Produces: `ScopeChoice = 'this'|'thisAndFollowing'`, plus delete mapper `this_and_following`.
- Produces: pay normal, pay occurrence, create weekly/monthly recurrence, reprice, scoped delete.

- [ ] **Step 1: Write failing payment and scope tests**

```typescript
expect(scopeToDeleteQuery('thisAndFollowing')).toBe('this_and_following');
expect(scopeToRepriceBody('thisAndFollowing')).toBe('thisAndFollowing');
```

Also assert a provisioned row opens occurrence payment while a normal pending row opens normal payment, and a fully paid provisioned row offers no payment action.

Run: `npm run test:ci`
Expected: FAIL with missing payment and scope behavior.

- [ ] **Step 2: Extend client/store with exact endpoints**

Add PATCH paid expense, POST `/expenses/{id}/pay-occurrence`, POST `/expenses/{id}/update-amount`, POST `/expense-recurrences`, and scoped DELETE. Wrap every successful write in `notifyingWrite`.

- [ ] **Step 3: Implement payment dialogs**

Normal payment collects actual amount, paid date and optional checking account. Occurrence payment pre-fills `occurrenceAmount`, displays progress and collects the same payment metadata. Disable duplicate submission while saving.

- [ ] **Step 4: Implement recurrence and scope flows**

Recurrence form toggles weekly/monthly and sends `weekday` or `dueDay`, never both. Reprice edits per-session amount. Scoped delete/reprice uses the shared stacked-choice dialog with explicit consequences.

- [ ] **Step 5: Verify and commit**

Run: `npm run test:ci && npm run build:prod`

```bash
git add src/app/features/expenses src/app/shared/dialogs
git commit -m "feat(expenses): add payments and recurring expense flows"
```

---

### Task 9: Gastos diários

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/daily-expenses/{daily-expenses.models.ts,daily-expenses.api.ts,daily-expenses.store.ts}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/daily-expenses/daily-expenses.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/daily-expenses/components/daily-expense-form/*`
- Modify: app routes
- Test: API/store/form specs

**Interfaces:**
- Produces: cycle-scoped daily expense list and create/update/delete actions.

- [ ] **Step 1: Write failing ordering, write-failure and form tests**

Use fixed ISO dates and assert newest first. A failed delete restores the removed row and does not bump `DataChangeService`.

Run: `npm run test:ci`
Expected: FAIL because the daily-expenses feature does not exist.

- [ ] **Step 2: Port DTOs and implement API/store**

Use `/daily-expenses` GET/POST/PATCH/DELETE and exact fields from backend `DailyExpenseDtos`. Load categories and checking accounts for the form.

- [ ] **Step 3: Implement dense page and form dialog**

Display date, description, category, account and amount; actions are explicit menu items. Form supports create/edit and preserves values on error.

- [ ] **Step 4: Verify and commit**

Run: `npm run test:ci && npm run build:prod`

```bash
git add src/app/features/daily-expenses src/app/app.routes.ts
git commit -m "feat(daily): add daily expense management"
```

---

### Task 10: Receitas e ajustes do ciclo

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/income.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/income.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/income.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/income.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/components/income-entry-form/income-entry-form.component.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/components/confirm-income-dialog/confirm-income-dialog.component.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/adjustments/adjustments.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/adjustments/adjustments.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/adjustments/adjustments.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/adjustments/adjustments.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/adjustments/components/adjustment-form/adjustment-form.component.{ts,html,scss}`
- Modify: app routes
- Test: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/income.{api,store}.spec.ts`
- Test: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/components/income-entry-form/income-entry-form.component.spec.ts`
- Test: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/income/components/confirm-income-dialog/confirm-income-dialog.component.spec.ts`
- Test: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/adjustments/adjustments.{api,store}.spec.ts`
- Test: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/adjustments/components/adjustment-form/adjustment-form.component.spec.ts`

**Interfaces:**
- Produces: income list/create/update/delete/confirm-received.
- Produces: adjustment list/create/update/delete and signed net projection.

- [ ] **Step 1: Write failing income tests**

Assert expected/received/late/not-occurred grouping, avulso create with null source, received confirmation payload, and failed confirmation retaining the row.

Run: `npm run test:ci`
Expected: FAIL because the income feature does not exist.

- [ ] **Step 2: Implement income API/store/page/forms**

Use `/income-entries` endpoints. Read existing sources for dropdown but do not expose source administration. Confirmation sends status `received`, actual amount and actual date.

- [ ] **Step 3: Write failing adjustment tests**

Assert inflow adds, outflow subtracts, list sorts newest first, and failed delete causes no global bump.

Run: `npm run test:ci`
Expected: FAIL because the adjustments feature does not exist.

- [ ] **Step 4: Implement adjustments API/store/page/form**

Use `/cycle-adjustments`; expose inflow/outflow in plain Portuguese. Display cycle net amount with negative styling that also includes a text sign.

- [ ] **Step 5: Verify and commit**

Run: `npm run test:ci && npm run build:prod`

```bash
git add src/app/features/income src/app/features/adjustments src/app/app.routes.ts
git commit -m "feat: add income and cycle adjustment flows"
```

---

### Task 11: Cartões, assinaturas e pagamento de fatura

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/cards/cards.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/cards/cards.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/cards/cards.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/cards/card-cycle.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/cards/cards.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/cards/components/{card-selector,statement-summary,installment-table,card-entry-form,pay-statement-dialog,reprice-subscription-dialog}/*`
- Modify: app routes
- Test: API/store/cycle/action/form specs

**Interfaces:**
- Produces: selected card/statement signals, bounded navigation, entry/subscription writes and statement payment.

- [ ] **Step 1: Write lifecycle and navigation tests**

```typescript
it.each([
  ['open', false],
  ['closed', true],
  ['paid', false],
] as const)('pay action for %s is %s', (status, expected) => {
  expect(canPayStatement(status)).toBe(expected);
});
```

Also test first/last statement bounds, other-card isolation and gaps between statement months using fixed dates.

Run: `npm run test:ci`
Expected: FAIL because card lifecycle helpers/store do not exist.

- [ ] **Step 2: Port card/statement contracts and implement API/store**

Use credit-card reads, card-entry CRUD, recurrence create, subscription update-amount/delete, card-statement reads and `/card-statements/{id}/pay`. Treat backend status as authoritative; do not derive `closed` in the browser.

- [ ] **Step 3: Implement selector, summary and installment table**

Cards are selected by an accessible segmented selector; statement month navigation disables at bounds. Show installment `N/M`, category, purchase date and amount. Paid statement shows paid date/account/actual amount.

- [ ] **Step 4: Implement purchase/subscription and payment flows**

Entry form supports one-off installments or monthly repeat, never both. Closed statements open payment dialog; open statements have no pay action; paid statements show read-only confirmation. Reprice/delete subscription reuse recurrence scope dialog.

- [ ] **Step 5: Verify and commit**

Run: `npm run test:ci && npm run build:prod`

```bash
git add src/app/features/cards src/app/app.routes.ts
git commit -m "feat(cards): add statements, subscriptions and payment"
```

---

### Task 12: Poupança

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/savings/savings.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/savings/savings.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/savings/savings.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/savings/savings.page.{ts,html,scss}`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/savings/components/{savings-selector,savings-summary,savings-transaction-form}/*`
- Modify: app routes
- Test: API/store/projection/form specs

**Interfaces:**
- Produces: selected account, `currentBalance`, signed cycle net flow and transaction CRUD.

- [ ] **Step 1: Write failing balance and selection tests**

Assert first account fallback, selected account persistence while available, deep-link selection, deposits positive, withdrawals negative and main number equal to backend `currentBalance` rather than cycle sum.

Run: `npm run test:ci`
Expected: FAIL because the savings feature does not exist.

- [ ] **Step 2: Implement contracts/API/store**

Read `/savings-accounts` and `/savings-transactions` by selected account + cycle. Use backend `currentBalance`; calculate only signed movement of currently loaded transactions.

- [ ] **Step 3: Implement page and transaction dialog**

Summary main label is `saldo da poupança`; subtitle is movement of cycle. Form supports aporte/saque, amount, date, label and optional checking-account linkage exactly as current DTO allows.

- [ ] **Step 4: Verify and commit**

Run: `npm run test:ci && npm run build:prod`

```bash
git add src/app/features/savings src/app/app.routes.ts
git commit -m "feat(savings): add current balance and transactions"
```

---

### Task 13: PWA, E2E, segurança estática e deploy preview

**Files:**
- Modify/Create via CLI: `/Users/jesstoselli/Repos/BillFolderWeb/ngsw-config.json`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/public/_headers`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/public/_redirects`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/playwright.config.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/e2e/fixtures/api.fixture.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/e2e/{auth,expense-payment,weekly-occurrence,statement-payment,home-refresh}.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/package.json`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/.github/workflows/ci.yml`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/README.md`

**Interfaces:**
- Produces: installable PWA shell with no API data caching.
- Produces: deterministic browser tests and Cloudflare-ready static build.

- [ ] **Step 1: Add PWA support and prove API is not cached**

Run:

```bash
npx ng add @angular/pwa@22 --project bill-folder-web --skip-confirmation
```

Keep asset groups for app shell/assets and remove all `dataGroups`. Add a test that parses `ngsw-config.json` and asserts no URL pattern contains `/v1/` or `api.billfolder.app`.

- [ ] **Step 2: Add Playwright and failing critical-flow specs**

```bash
npm install --save-dev @playwright/test
npx playwright install chromium
```

API fixture must mock deterministic JSON at the network boundary, including Set-Cookie-equivalent authenticated state via the refresh response. Specs cover login restore, creating a one-off expense, paying weekly occurrence, paying closed statement and observing updated Home without page reload.

- [ ] **Step 3: Implement static hosting headers and SPA fallback**

`public/_redirects`:

```text
/* /index.html 200
```

`public/_headers` sets `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin`, restrictive `Permissions-Policy`, frame denial and CSP allowing only self-hosted scripts/styles/fonts, data URLs for generated icons, and `https://api.billfolder.app` for connections.

- [ ] **Step 4: Extend CI with browser tests**

Add Chromium install and `npm run e2e` after unit tests/build. Upload Playwright report only on failure. Add scripts `e2e` and `e2e:ui`.

- [ ] **Step 5: Document local and Cloudflare setup**

README includes Node requirement, install/test/build commands, local API URL, production output `dist/bill-folder-web/browser`, Cloudflare build command `npm run build:prod`, and explicit note that production setup requires user authorization.

- [ ] **Step 6: Run final local verification**

Run:

```bash
npm ci
npm run test:ci
npm run build:prod
npm run e2e
```

Expected: all unit and E2E tests pass; PWA manifest/service worker present; no API dataGroups.

- [ ] **Step 7: Commit release readiness**

```bash
git add .
git commit -m "chore: prepare BillFolder Web PWA for preview deployment"
```

- [ ] **Step 8: Stop for explicit deployment authorization**

After the user approves: create/connect the GitHub repository, configure Cloudflare Pages with branch `main`, command `npm run build:prod`, directory `dist/bill-folder-web/browser`, add `app.billfolder.app`, set backend `WebAuth:AllowedOrigins`, deploy backend first, then web, and run authenticated smoke tests. Do not perform this step from plan execution without that approval.

---

### Task 14: Whole-MVP review and handoff to parity

**Files:**
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/README.md`
- Modify: `/Users/jesstoselli/Repos/BillFolder-Session-Handoff.md`
- Create: `/Users/jesstoselli/Repos/BillFolder/docs/superpowers/specs/2026-10-07-billfolder-web-parity-backlog.md`

**Interfaces:**
- Produces: verified MVP status, deployment runbook and bounded parity backlog.

- [ ] **Step 1: Run backend and frontend full suites from clean state**

```bash
cd /Users/jesstoselli/Repos/BillFolder && dotnet test && dotnet build --no-incremental
cd /Users/jesstoselli/Repos/BillFolderWeb && npm ci && npm run test:ci && npm run build:prod && npm run e2e
```

Record exact counts and failures; do not claim completion from earlier task runs.

- [ ] **Step 2: Perform accessibility and responsive smoke review**

Check keyboard-only operation, visible focus, dialogs returning focus, 320px/768px/1200px layouts, zoom at 200%, reduced motion and status text independent of color. Fix any failure before continuing.

- [ ] **Step 3: Request whole-feature code review**

Review cross-repo auth contract, cookie/CORS policy, token exposure, data-change loops, date handling, DTO drift and every write path. Resolve Important/Critical findings and rerun affected suites.

- [ ] **Step 4: Write parity backlog as a separate approved design seed**

List exact deferred modules: cycles, checking accounts, cards, savings accounts, income sources, onboarding, preferences, light theme and optional Tauri shell. State that offline sync and push remain separate product decisions, not implicit parity.

- [ ] **Step 5: Update handoff and commit documentation**

```bash
cd /Users/jesstoselli/Repos/BillFolderWeb
git add README.md
git commit -m "docs: finalize BillFolder Web MVP runbook"

cd /Users/jesstoselli/Repos/BillFolder
git add docs/superpowers/specs/2026-10-07-billfolder-web-parity-backlog.md
git commit -m "docs: define BillFolder Web parity backlog"
```

The root handoff update is committed only in whichever repository owns that file at execution time; if it remains outside Git, report it as an uncommitted workspace document instead of silently omitting it.
