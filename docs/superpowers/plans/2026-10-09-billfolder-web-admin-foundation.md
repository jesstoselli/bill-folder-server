# BillFolder Web Admin Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Atualizar a base documental e de qualidade do web e entregar administração segura de Ciclos e Contas-correntes sem perda silenciosa de dados.

**Architecture:** O web ganha duas páginas lazy-loaded sob uma nova seção “Gerenciar”, reutilizando o shell, os componentes compartilhados e stores específicos por domínio. `CycleStore` passa a concentrar também os writes de ciclos; contas-correntes ganham API/modelos próprios em `core/checking-accounts`, um store administrativo e invalidação explícita do cache de referências. A API bloqueia a exclusão de conta com dependências destrutivas ou restritivas usando `409 account_in_use`.

**Tech Stack:** Angular 22.2 standalone components, typed reactive forms, Signals, Angular Material dialogs, RxJS 7.8, Vitest, Playwright, SCSS; .NET 10, EF Core, PostgreSQL, FluentValidation, xUnit/Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-09-billfolder-web-admin-foundation-design.md`

**Status:** implementado e verificado localmente; aguardando autorização de commit/push.

## Global Constraints

- Trabalhar nos checkouts atuais em `main`; não criar worktree, branch, push, deploy ou publicação sem autorização explícita.
- Não executar commits durante a implementação sem pedido explícito da usuária; as mensagens sugeridas abaixo são apenas checkpoints para quando houver autorização.
- Node.js mínimo `24.15.0` e npm `11.12.1`; executar `nvm use` antes dos gates web.
- Manter o web online-only para dados financeiros e preservar o contrato de autenticação por cookie `HttpOnly`.
- Preservar a direção visual “Painel focado”, os tokens existentes e a linguagem já usada no Android.
- Não expor o endpoint técnico de backfill nesta entrega.
- Não inventar bloqueio de sobreposição de ciclos apenas no cliente; a API hoje garante `startDate < endDate` e início único.
- Não criar abstração genérica `CrudStore<T>` nem formulário CRUD universal.
- Todo comportamento novo segue RED → GREEN → REFACTOR; configuração/documentação usa comando de verificação falhando antes da correção.
- Writes devem usar `notifyingWrite`; falhas não notificam o barramento global.
- Não adicionar dependências npm ou NuGet.

## Review Focus

1. **Conta em uso:** poupança, despesa avulsa ou recorrência vinculada deve produzir `409 account_in_use`, sem remover a conta nem dependências.
2. **Troca de principal:** marcar uma conta como principal deve remover visualmente o badge da anterior após o reload e invalidar seletores em cache.
3. **Ciclo selecionado removido:** o `CycleStore` deve cair para o ciclo atual retornado pelo backend, ou para `null` quando nenhum ciclo cobre hoje, sem manter seleção fantasma.
4. **Criação de ciclo com expansão:** como o backend pode gerar onze ciclos futuros, a UI deve recarregar a coleção completa em vez de apenas inserir a resposta do POST.
5. **Concorrência de refresh/write:** uma resposta antiga não pode sobrescrever uma listagem mais nova, e submissão duplicada precisa continuar bloqueada pelo `WriteDialogLock`.

---

### Task 1: Higiene, formatação e documentação do rollout atual

**Files:**
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/package.json`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/scripts/verify-node-version.mjs`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/main.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/tsconfig.app.json`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/tsconfig.spec.json`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/wrangler.jsonc`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/README.md`
- Modify: `/Users/jesstoselli/Repos/BillFolder-Session-Handoff.md`
- Modify: `/Users/jesstoselli/Repos/BillFolder/docs/superpowers/specs/2026-10-07-billfolder-web-parity-backlog.md`

**Interfaces:**
- Consumes: scripts npm existentes `test:ci`, `build:prod`, `build:preview`, `e2e`.
- Produces: `npm run format`, `npm run format:check`, README/handoff alinhados ao rollout real e uma tabela de status de paridade.

- [ ] **Step 1: Registrar o RED do baseline de formatação**

Run:

```bash
cd /Users/jesstoselli/Repos/BillFolderWeb
npx prettier --check scripts/verify-node-version.mjs src/main.ts tsconfig.app.json tsconfig.spec.json wrangler.jsonc
```

Expected: FAIL listando exatamente os cinco arquivos conhecidos.

- [ ] **Step 2: Adicionar scripts estáveis de formatação**

Modificar `package.json`:

```json
{
  "scripts": {
    "format": "prettier --write .",
    "format:check": "prettier --check .",
    "test:ci": "npm run format:check && ng test --watch=false && npm run test:static"
  }
}
```

Preservar todos os demais scripts e sua ordem lógica.

- [ ] **Step 3: Formatar somente o baseline conhecido e validar o gate global**

Run:

```bash
npx prettier --write scripts/verify-node-version.mjs src/main.ts tsconfig.app.json tsconfig.spec.json wrangler.jsonc
npm run format:check
```

Expected: PASS sem arquivos pendentes.

- [ ] **Step 4: Atualizar README e handoff com fatos já ocorridos**

Registrar, sem linguagem de “aguardando autorização”:

```markdown
- remote GitHub conectado e pushes realizados pela usuária;
- migration `20261008120000_AddRefreshTokenFamilies` aplicada e API reiniciada;
- CORS de `https://preview.billfolder.app` validado;
- Cloudflare Worker `bill-folder-web-preview` conectado à `main` com build automático;
- domínio `https://preview.billfolder.app` ativo e autenticação/recuperação de senha validadas;
- deploy continua fora da autorização implícita de futuras mudanças.
```

Não atualizar ainda os números finais de testes desta entrega; isso ocorre na Task 8 com evidência nova.

- [ ] **Step 5: Atualizar o backlog com status operacional anterior à implementação**

Adicionar uma tabela no início da seção 3:

```markdown
| Módulo | Infraestrutura/API | UI administrativa web |
| --- | --- | --- |
| Ciclos | pronta | em implementação nesta entrega |
| Contas-correntes | pronta | em implementação nesta entrega |
| Contas de cartão | pronta | pendente |
| Contas de poupança | pronta | pendente |
| Fontes de renda | pronta | pendente |
```

- [ ] **Step 6: Validar documentação e configuração**

Run:

```bash
npm run format:check
npm run test:static
git diff --check
```

Expected: todos aprovados e nenhuma referência afirmando que preview/remote/deploy nunca existiram.

- [ ] **Step 7: Checkpoint sem commit**

Inspecionar:

```bash
git diff -- package.json scripts/verify-node-version.mjs src/main.ts tsconfig.app.json tsconfig.spec.json wrangler.jsonc README.md
```

Suggested commit if later authorized: `chore(web): atualiza baseline de qualidade e rollout`

---

### Task 2: Bloquear exclusão destrutiva de conta-corrente na API

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolder/tests/BillFolder.Api.Tests/Accounts/CheckingAccountDeletionTests.cs`
- Modify: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Application/UseCases/Accounts/CheckingAccountsService.cs`
- Modify: `/Users/jesstoselli/Repos/BillFolder/src/BillFolder.Api/Endpoints/CheckingAccountsEndpoints.cs`

**Interfaces:**
- Consumes: `IApplicationDbContext.BeginTransactionAsync`, `SavingsAccounts`, `DailyExpenses`, `DailyExpenseRecurrences`.
- Produces: `DeleteAsync` retorna `OperationResult.Fail<bool>("account_in_use", message)` para dependências bloqueadoras; endpoint traduz para HTTP 409.

- [ ] **Step 1: Escrever testes de integração RED para a matriz de dependências**

Criar `CheckingAccountDeletionTests` com os nomes exatos
`Delete_UnusedAccount_RemovesIt` e
`Delete_AccountInUse_ReturnsConflictWithoutRemovingData`. O segundo é uma theory com os valores
`SavingsAccount`, `DailyExpense` e `DailyExpenseRecurrence` do enum privado
`BlockingDependency`.

Usar `ApiWebApplicationFactory`. O helper `SignupAsync` deve postar um email único em `/v1/auth/signup`, desserializar `AuthResponse` e devolver `User.Id` + `AccessToken`. O helper `CreateAccountAsync` deve postar em `/v1/checking-accounts/` com `Authorization: Bearer`, devolver o `CheckingAccountResponse` e limpar o header ao terminar. Para cada valor de `BlockingDependency`, abrir um scope de `ApplicationDbContext`, consultar uma categoria do usuário quando necessária e inserir exatamente uma destas entidades com o `accountId` criado: `SavingsAccount.CheckingAccountId`, `DailyExpense.AccountId` ou `DailyExpenseRecurrence.DefaultAccountId`. Persistir, executar o DELETE autenticado e consultar a mesma dependência em um scope novo para provar que ela continua presente.

Asserções obrigatórias:

```csharp
response.StatusCode.Should().Be(HttpStatusCode.Conflict);
problem!.Error.Should().Be("account_in_use");
(await db.CheckingAccounts.AnyAsync(x => x.Id == accountId)).Should().BeTrue();
```

- [ ] **Step 2: Executar e confirmar RED**

Run:

```bash
cd /Users/jesstoselli/Repos/BillFolder
dotnet test tests/BillFolder.Api.Tests/BillFolder.Api.Tests.csproj \
  --filter FullyQualifiedName~CheckingAccountDeletionTests
```

Expected: os casos com dependência falham porque a conta é apagada ou a constraint gera resposta diferente de 409.

- [ ] **Step 3: Implementar a verificação dentro de transação**

Em `CheckingAccountsService.DeleteAsync`:

```csharp
await using var transaction = await _db.BeginTransactionAsync(ct);

var account = await _db.CheckingAccounts
    .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, ct);
if (account is null)
{
    return OperationResult.Fail<bool>("not_found", "Conta não encontrada.");
}

var inUse = await _db.SavingsAccounts.AnyAsync(
        a => a.UserId == userId && a.CheckingAccountId == accountId,
        ct)
    || await _db.DailyExpenses.AnyAsync(
        e => e.UserId == userId && e.AccountId == accountId,
        ct)
    || await _db.DailyExpenseRecurrences.AnyAsync(
        r => r.UserId == userId && r.DefaultAccountId == accountId,
        ct);
if (inUse)
{
    return OperationResult.Fail<bool>(
        "account_in_use",
        "Esta conta ainda está vinculada a uma poupança ou despesa avulsa. Remova ou altere esses vínculos antes de excluir.");
}

_db.CheckingAccounts.Remove(account);
await _db.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
return OperationResult.Ok(true);
```

Não bloquear referências `SetNull`, pois elas preservam o lançamento histórico.

- [ ] **Step 4: Mapear o erro para HTTP 409**

Em `CheckingAccountsEndpoints.ToHttpResult`:

```csharp
"account_in_use" => StatusCodes.Status409Conflict,
```

- [ ] **Step 5: Executar GREEN e suíte backend**

Run:

```bash
dotnet test tests/BillFolder.Api.Tests/BillFolder.Api.Tests.csproj \
  --filter FullyQualifiedName~CheckingAccountDeletionTests
dotnet test tests/BillFolder.Api.Tests/BillFolder.Api.Tests.csproj
dotnet build BillFolder.slnx --no-incremental
```

Expected: todos os testes aprovados; build com 0 warnings e 0 errors.

- [ ] **Step 6: Checkpoint sem commit**

Suggested commit if later authorized: `fix(accounts): impede exclusão destrutiva de conta em uso`

---

### Task 3: Criar fundação visual e navegação administrativa compartilhada

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/dialogs/confirm-action-dialog/confirm-action-dialog.component.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/dialogs/confirm-action-dialog/confirm-action-dialog.component.html`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/dialogs/confirm-action-dialog/confirm-action-dialog.component.scss`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/dialogs/confirm-action-dialog/confirm-action-dialog.component.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/components/icon/icon.component.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/components/icon/icon.component.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/layout/sidebar.component.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/layout/sidebar.component.html`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/layout/sidebar.component.scss`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/layout/sidebar.component.spec.ts`

**Interfaces:**
- Produces: `ConfirmActionDialogData`, `ConfirmActionDialogComponent`, ícones `calendarMonth` e `accountBalance` e grupos de sidebar.
- Consumes later: páginas das Tasks 4 e 6.

- [ ] **Step 1: Escrever RED do diálogo compartilhado**

Testar com as APIs DOM já usadas no projeto:

```typescript
const root = fixture.nativeElement as HTMLElement;
expect(root.querySelector('h2')?.textContent).toContain('Excluir conta?');

const buttons = [...root.querySelectorAll<HTMLButtonElement>('button')];
buttons.find((button) => button.textContent?.includes('Excluir'))!.click();
expect(close).toHaveBeenCalledWith(true);

buttons.find((button) => button.textContent?.includes('Cancelar'))!.click();
expect(close).toHaveBeenCalledWith(false);
```

O contrato de dados é:

```typescript
export interface ConfirmActionDialogData {
  readonly title: string;
  readonly message: string;
  readonly confirmLabel: string;
  readonly cancelLabel?: string;
}
```

- [ ] **Step 2: Confirmar RED do diálogo**

Run:

```bash
npm test -- --watch=false --include='src/app/shared/dialogs/confirm-action-dialog/*.spec.ts'
```

Expected: FAIL porque o componente ainda não existe.

- [ ] **Step 3: Implementar o diálogo mínimo e acessível**

Usar `MatDialogModule` e `ButtonComponent`. O botão destrutivo usa:

```html
<button type="button" appButton variant="secondary" tone="danger" (click)="confirm()">
  {{ data.confirmLabel }}
</button>
```

O componente fecha com `true` ou `false`; ele não executa regra de domínio nem request HTTP.

- [ ] **Step 4: Escrever RED dos grupos, destinos e ícones**

Atualizar os testes para esperar:

```typescript
expect(sectionLabels).toEqual(['Principal', 'Gerenciar']);
expect(linkLabels).toContain('Ciclos');
expect(linkLabels).toContain('Contas-correntes');
expect(iconNames).toContain('calendarMonth');
expect(iconNames).toContain('accountBalance');
```

- [ ] **Step 5: Confirmar RED de navegação**

Run:

```bash
npm test -- --watch=false --include='src/app/core/layout/sidebar.component.spec.ts' \
  --include='src/app/shared/components/icon/icon.component.spec.ts'
```

Expected: FAIL por destinos e ícones ausentes.

- [ ] **Step 6: Implementar grupos de sidebar**

Modelar:

```typescript
interface NavigationSection {
  readonly label: string;
  readonly destinations: readonly NavigationDestination[];
}
```

- [ ] **Step 7: Adicionar vetores locais dos dois ícones e estilos de seção**

Adicionar `calendarMonth` e `accountBalance` em `BILL_FOLDER_ICON_PATHS`, usando os vetores Material Filled locais equivalentes ao Android. O título de seção deve ser discreto, sem uppercase forçado, e oculto visualmente no modo rail.

- [ ] **Step 8: Executar GREEN da fundação compartilhada**

Run:

```bash
npm test -- --watch=false --include='src/app/shared/dialogs/confirm-action-dialog/*.spec.ts' \
  --include='src/app/core/layout/sidebar.component.spec.ts' \
  --include='src/app/shared/components/icon/icon.component.spec.ts'
```

Expected: PASS.

- [ ] **Step 9: Checkpoint sem commit**

Suggested commit if later authorized: `feat(navigation): adiciona fundação administrativa`

---

### Task 4: Entregar CRUD web de Ciclos sobre o CycleStore compartilhado

**Files:**
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/cycles/cycle.models.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/cycles/cycles.api.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/cycles/cycles.api.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/cycles/cycle.store.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/cycles/cycle.store.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.routes.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.routes.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/forms/validators.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/shared/forms/validators.spec.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-cycles/manage-cycles.page.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-cycles/manage-cycles.page.html`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-cycles/manage-cycles.page.scss`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-cycles/manage-cycles.page.spec.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-cycles/components/cycle-form/cycle-form.component.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-cycles/components/cycle-form/cycle-form.component.html`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-cycles/components/cycle-form/cycle-form.component.scss`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-cycles/components/cycle-form/cycle-form.component.spec.ts`

**Interfaces:**
- Produces: `CreateCycleRequest`, `UpdateCycleRequest`, `CyclesApi.create/update/delete`, `CycleStore.create/update/delete/refresh`, `CycleFormComponent`, `ManageCyclesPage`.
- Consumes: `ConfirmActionDialogComponent`, `DialogFrameComponent`, `WriteDialogLock`, `RowFocus`, `bf-ledger`.

- [ ] **Step 1: Escrever RED da API de ciclos**

Adicionar testes que afirmem:

```typescript
api.create({ startDate: '2026-11-01', endDate: '2026-11-30', label: 'novembro/2026' });
expectOne('/v1/cycles/').request.method === 'POST';

api.update('cycle-1', { label: 'novo nome' });
expectOne('/v1/cycles/cycle-1').request.method === 'PATCH';

api.delete('cycle-1');
expectOne('/v1/cycles/cycle-1').request.method === 'DELETE';
```

Também provar que `DataChangeService.version()` só aumenta em respostas de sucesso.

- [ ] **Step 2: Confirmar RED e implementar API/modelos**

Run RED:

```bash
npm test -- --watch=false --include='src/app/core/cycles/cycles.api.spec.ts'
```

Implementar:

```typescript
export interface CreateCycleRequest {
  readonly startDate: string;
  readonly endDate: string;
  readonly label: string;
}

export type UpdateCycleRequest = Partial<CreateCycleRequest>;
```

Todos os writes usam `notifyingWrite(...pipe(mapApiErrors()))`.

- [ ] **Step 3: Escrever RED do CycleStore após writes**

Cobrir:

```typescript
it('reloads the whole list after create because the backend can expand future cycles');
it('falls back to backend current after deleting the selected cycle');
it('keeps previous content while refresh fails');
it('does not let an older load overwrite a newer write reload');
```

- [ ] **Step 4: Confirmar RED e implementar writes no CycleStore**

Run RED:

```bash
npm test -- --watch=false --include='src/app/core/cycles/cycle.store.spec.ts'
```

Adicionar:

```typescript
refresh(): Promise<void> {
  return this.load();
}

async create(request: CreateCycleRequest): Promise<CycleResponse> {
  const saved = await firstValueFrom(this.api.create(request));
  await this.load();
  return saved;
}

async update(id: string, request: UpdateCycleRequest): Promise<CycleResponse> {
  const saved = await firstValueFrom(this.api.update(id, request));
  await this.load();
  return saved;
}

async delete(id: string): Promise<void> {
  await firstValueFrom(this.api.delete(id));
  await this.load();
}
```

Reusar `loadGeneration`; não inserir apenas a resposta do POST.

- [ ] **Step 5: Escrever RED do formulário**

Casos obrigatórios:

```typescript
it('defaults create mode to the current civil month and pt-BR lowercase label');
it('prefills every field in edit mode');
it('rejects blank label and end date not after start date');
it('submits the exact create or patch request once while saving');
it('shows duplicate_start_date without closing the dialog');
```

Injetar a data atual por uma função pura exportada para o teste, evitando depender do relógio real:

```typescript
export function cycleDefaults(today: string): CreateCycleRequest;
```

- [ ] **Step 6: Escrever RED e implementar o validador compartilhado de intervalo**

Em `validators.spec.ts`:

```typescript
const form = new FormGroup({
  startDate: control('2026-11-30'),
  endDate: control('2026-11-01'),
}, { validators: dateRange('startDate', 'endDate') });
expect(form.errors).toEqual({ dateRange: true });
```

Implementar:

```typescript
export function dateRange(startKey: string, endKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start = group.get(startKey);
    const end = group.get(endKey);
    if (
      typeof start?.value !== 'string' ||
      typeof end?.value !== 'string' ||
      !start.value ||
      !end.value ||
      start.errors !== null ||
      end.errors !== null
    ) {
      return null;
    }

    return start.value < end.value ? null : { dateRange: true };
  };
}
```

Ela retorna `null` quando algum campo está vazio ou individualmente inválido, deixando `required` e `validCivilDate` cuidarem desses erros; retorna `{ dateRange: true }` somente quando as duas datas civis válidas não obedecem `start < end`.

- [ ] **Step 7: Implementar formulário com typed reactive form**

Form:

```typescript
readonly form = this.formBuilder.nonNullable.group({
  label: ['', [Validators.required, nonBlank, Validators.maxLength(100)]],
  startDate: ['', [Validators.required, validCivilDate]],
  endDate: ['', [Validators.required, validCivilDate]],
}, { validators: [dateRange('startDate', 'endDate')] });
```

Usar `todayCivilDate()` como entrada de `cycleDefaults`, evitando UTC e relógio não controlado nos testes.

- [ ] **Step 8: Escrever RED da página de gerenciamento e da rota lazy**

Testar estados, badges, confirmação e foco:

```typescript
expect(row.textContent).toContain('Atual');
expect(row.textContent).toContain('Gerado automaticamente');
expect(deleteDialog.message).toContain('pode deixar o app sem um ciclo atual');
expect(cycles.delete).not.toHaveBeenCalledBeforeConfirmation();
```

Incluir caso de erro 409/400 preservando a lista e devolvendo foco ao botão da linha.

Em `app.routes.spec.ts`, exigir que `/gerenciar/ciclos` use `loadComponent` e resolva para `ManageCyclesPage`.

- [ ] **Step 9: Implementar a página e a rota com o padrão bf-ledger**

Colunas desktop: Ciclo, Período, Origem/estado, Ações. No mobile o `bf-ledger` transforma cada linha em card. Registrar `CycleStore` com `registerActiveRouteRefresh` e usar `RowFocus` para edit/delete.

Após confirmação:

```typescript
await this.cycles.delete(cycle.id);
this.rowFocus.afterDelete(focus, true);
```

Em erro, mapear via `mapApiError`, manter conteúdo e restaurar foco.

Adicionar:

```typescript
{
  path: 'gerenciar/ciclos',
  loadComponent: () =>
    import('./features/manage-cycles/manage-cycles.page').then(({ ManageCyclesPage }) => ManageCyclesPage),
  data: { title: 'Ciclos' },
}
```

- [ ] **Step 10: Executar GREEN do módulo de ciclos**

Run:

```bash
npm test -- --watch=false --include='src/app/core/cycles/*.spec.ts' \
  --include='src/app/features/manage-cycles/**/*.spec.ts' \
  --include='src/app/shared/forms/validators.spec.ts' \
  --include='src/app/app.routes.spec.ts'
```

Expected: PASS.

- [ ] **Step 11: Checkpoint sem commit**

Suggested commit if later authorized: `feat(cycles): adiciona administração web de ciclos`

---

### Task 5: Extrair o domínio de Contas-correntes e invalidar referências

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/checking-accounts/checking-account.models.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/checking-accounts/checking-accounts.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/checking-accounts/checking-accounts.api.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/reference/reference-data.api.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/reference/reference-data.api.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/reference/reference-data.store.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/reference/reference-data.store.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/core/reference/reference-ordering.ts`
- Modify: imports in payment and daily-expense dialogs that currently import `CheckingAccountResponse` from `reference-data.api.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/manage-accounts.store.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/manage-accounts.store.spec.ts`

**Interfaces:**
- Produces: `CheckingAccountResponse`, `CreateCheckingAccountRequest`, `UpdateCheckingAccountRequest`, `CheckingAccountsApi`, `ReferenceDataStore.invalidateCheckingAccounts()`, `ManageAccountsStore`.
- Consumes later: formulário e página da Task 6.

- [ ] **Step 1: Escrever RED da API dedicada**

Testar os cinco contratos:

```typescript
list(): GET /v1/checking-accounts/
get(id): GET /v1/checking-accounts/{id}
create(body): POST /v1/checking-accounts/
update(id, body): PATCH /v1/checking-accounts/{id}
delete(id): DELETE /v1/checking-accounts/{id}
```

Os três writes devem notificar uma vez no sucesso e zero no erro.

- [ ] **Step 2: Confirmar RED e implementar modelos/API**

Modelos:

```typescript
export interface CreateCheckingAccountRequest {
  readonly bankName: string;
  readonly branch: string;
  readonly accountNumber: string;
  readonly initialBalance: number;
  readonly isPrimary: boolean;
}

export type UpdateCheckingAccountRequest = Partial<CreateCheckingAccountRequest>;
```

Mover `CheckingAccountResponse` para esse arquivo e atualizar imports sem duplicar a interface.

- [ ] **Step 3: Escrever RED da invalidação de cache**

Em `reference-data.store.spec.ts`:

```typescript
const api = TestBed.inject(CheckingAccountsApi);
vi.spyOn(api, 'list');
await store.checkingAccounts();
store.invalidateCheckingAccounts();
await store.checkingAccounts();
expect(api.list).toHaveBeenCalledTimes(2);
```

Também provar que invalidar contas não invalida categorias.

- [ ] **Step 4: Implementar `SortedListCache.invalidate()`**

```typescript
invalidate(): void {
  this.fetchedAt = 0;
  this.value = null;
}
```

`ReferenceDataStore` passa a injetar `CheckingAccountsApi` para o cache de contas e expõe somente `invalidateCheckingAccounts()` publicamente.

- [ ] **Step 5: Escrever RED do ManageAccountsStore**

Cobrir:

```typescript
it('sorts primary first after load');
it('reloads every account after making another account primary');
it('invalidates reference cache after every successful write');
it('keeps content visible when refresh fails');
it('keeps the row and reports account_in_use when delete fails');
it('ignores an older load response after a newer refresh');
```

- [ ] **Step 6: Implementar o store específico**

Estado:

```typescript
private readonly stateValue = signal<LoadState<CheckingAccountResponse[]>>({ kind: 'loading' });
readonly state = this.stateValue.asReadonly();
readonly accounts = computed(() => this.stateValue().kind === 'content' ? this.stateValue().data : []);
```

Writes devem invalidar o cache e recarregar a lista completa. Não atualizar só a linha quando `isPrimary` pode ter alterado outra conta.

- [ ] **Step 7: Executar GREEN do domínio de contas**

Run:

```bash
npm test -- --watch=false --include='src/app/core/checking-accounts/*.spec.ts' \
  --include='src/app/core/reference/*.spec.ts' \
  --include='src/app/features/manage-accounts/manage-accounts.store.spec.ts'
```

Expected: PASS.

- [ ] **Step 8: Checkpoint sem commit**

Suggested commit if later authorized: `refactor(accounts): centraliza contratos e cache de contas`

---

### Task 6: Entregar formulários e página de Contas-correntes

**Files:**
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/manage-accounts.page.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/manage-accounts.page.html`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/manage-accounts.page.scss`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/manage-accounts.page.spec.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/components/checking-account-form/checking-account-form.component.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/components/checking-account-form/checking-account-form.component.html`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/components/checking-account-form/checking-account-form.component.scss`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/features/manage-accounts/components/checking-account-form/checking-account-form.component.spec.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.routes.ts`
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/src/app/app.routes.spec.ts`

**Interfaces:**
- Consumes: `ManageAccountsStore`, `CheckingAccountResponse`, `ConfirmActionDialogComponent`, `DialogFrameComponent`, `MoneyInputDirective`, `WriteDialogLock`, `RowFocus`.
- Produces: CRUD completo e responsivo em `/gerenciar/contas`.

- [ ] **Step 1: Escrever RED do formulário**

Casos:

```typescript
it('starts create mode with zero balance and no primary flag');
it('prefills edit mode including primary state');
it('rejects blank bank, branch, account number and negative balance');
it('trims text and sends a complete create request');
it('sends a partial-compatible update request with every editable field');
it('prevents a second submit while the first request is pending');
```

- [ ] **Step 2: Confirmar RED e implementar formulário**

Typed form:

```typescript
readonly form = this.formBuilder.nonNullable.group({
  bankName: ['', [Validators.required, nonBlank, Validators.maxLength(100)]],
  branch: ['', [Validators.required, nonBlank, Validators.maxLength(20)]],
  accountNumber: ['', [Validators.required, nonBlank, Validators.maxLength(30)]],
  initialBalance: [0, [Validators.required, Validators.min(0)]],
  isPrimary: [false],
});
```

Usar `MoneyInputDirective`, `MatSlideToggleModule` e texto: “Ao marcar, esta conta substitui a principal atual.”

- [ ] **Step 3: Escrever RED da página e da rota lazy**

Casos:

```typescript
it('renders primary first with bank, branch/account and explicit initial balance');
it('opens create and edit dialogs and restores row focus');
it('asks for confirmation before delete');
it('keeps the row and shows the backend account_in_use message after 409');
it('disables the row delete action while the request is pending');
it('renders loading, initial error, refresh error and empty states');
```

Em `app.routes.spec.ts`, exigir que `/gerenciar/contas` use `loadComponent` e resolva para `ManageAccountsPage`.

- [ ] **Step 4: Implementar página, rota e copy destrutiva**

Tabela desktop: Conta, Agência / número, Saldo inicial, Estado, Ações. Badge textual “Principal”.

Mensagem de confirmação:

```text
Excluir esta conta remove o cadastro e desfaz referências históricas permitidas. Se houver poupança ou despesa avulsa vinculada, a exclusão será bloqueada para proteger seus dados.
```

No `account_in_use`, mostrar a mensagem da API no `InlineAlertComponent`; não remover a linha.

Adicionar:

```typescript
{
  path: 'gerenciar/contas',
  loadComponent: () =>
    import('./features/manage-accounts/manage-accounts.page').then(({ ManageAccountsPage }) => ManageAccountsPage),
  data: { title: 'Contas-correntes' },
}
```

- [ ] **Step 5: Garantir refresh ativo e foco**

Registrar `ManageAccountsStore` com `registerActiveRouteRefresh`. Usar `RowFocus` com `data-checking-account-id`, ações `edit`/`delete` e target do botão “Nova conta”.

- [ ] **Step 6: Executar GREEN da UI de contas**

Run:

```bash
npm test -- --watch=false --include='src/app/features/manage-accounts/**/*.spec.ts'
npm test -- --watch=false --include='src/app/app.routes.spec.ts'
```

Expected: PASS.

- [ ] **Step 7: Checkpoint sem commit**

Suggested commit if later authorized: `feat(accounts): adiciona administração web de contas-correntes`

---

### Task 7: Validar os fluxos administrativos no navegador

**Files:**
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/e2e/fixtures/api.fixture.ts`
- Create: `/Users/jesstoselli/Repos/BillFolderWeb/e2e/admin-foundation.spec.ts`

**Interfaces:**
- Consumes: rotas, forms e APIs das Tasks 3–6.
- Produces: fixture determinística mutável para ciclos/contas e smoke E2E de CRUD/integração.

- [ ] **Step 1: Escrever RED E2E de ciclos**

Fluxo:

```typescript
await page.goto('/gerenciar/ciclos');
await page.getByRole('button', { name: 'Novo ciclo' }).click();
await page.getByLabel('Nome').fill('novembro/2026');
await page.getByLabel('Data inicial').fill('2026-11-01');
await page.getByLabel('Data final').fill('2026-11-30');
await page.getByRole('button', { name: 'Salvar ciclo' }).click();
await expect(page.getByText('novembro/2026')).toBeVisible();
```

Depois editar o nome, excluir com confirmação e verificar que o request body/método foi registrado.

- [ ] **Step 2: Escrever RED E2E de contas e seletor consumidor**

Criar uma conta, marcá-la principal, navegar para pagamento de despesa e provar que ela aparece no seletor sem reload do navegador. Em seguida, simular `409 account_in_use` no DELETE e provar que a linha continua visível com a mensagem da API.

- [ ] **Step 3: Confirmar RED**

Run:

```bash
npm run e2e -- --grep 'admin foundation'
```

Expected: FAIL porque a fixture ainda responde `unexpected_e2e_request` para os novos writes.

- [ ] **Step 4: Estender a fixture com estado mutável**

Adicionar arrays mutáveis de `cycles` e `checkingAccounts` e suportar:

```text
GET/POST /v1/cycles/
PATCH/DELETE /v1/cycles/{id}
GET/POST /v1/checking-accounts/
PATCH/DELETE /v1/checking-accounts/{id}
```

No PATCH de conta principal, desmarcar as demais. O DELETE de uma conta marcada pela fixture como em uso retorna:

```json
{
  "error": "account_in_use",
  "message": "Esta conta ainda está vinculada a uma poupança ou despesa avulsa. Remova ou altere esses vínculos antes de excluir."
}
```

- [ ] **Step 5: Executar GREEN E2E e revisar responsividade**

Run:

```bash
npm run e2e -- --grep 'admin foundation'
```

Repetir os pontos críticos em viewports 320, 768 e 1200 px no teste, verificando ausência de overflow horizontal e acesso às ações por teclado.

- [ ] **Step 6: Inspecionar visualmente as duas páginas**

Gerar screenshots Playwright em 320 px e 1200 px para Ciclos e Contas-correntes, abrir as quatro imagens e conferir:

```text
- hierarquia compatível com “Painel focado”;
- headings e ações alinhados;
- badges legíveis sem dominar a linha;
- ações acessíveis no formato de card mobile;
- nenhum segundo sistema visual de cards/tokens.
```

Corrigir somente problemas observados, com teste de regressão antes de comportamento alterado.

- [ ] **Step 7: Checkpoint sem commit**

Suggested commit if later authorized: `test(web): cobre administração de ciclos e contas`

---

### Task 8: Fechar documentação, paridade e gates completos

**Files:**
- Modify: `/Users/jesstoselli/Repos/BillFolderWeb/README.md`
- Modify: `/Users/jesstoselli/Repos/BillFolder-Session-Handoff.md`
- Modify: `/Users/jesstoselli/Repos/BillFolder/docs/superpowers/specs/2026-10-07-billfolder-web-parity-backlog.md`
- Modify: `/Users/jesstoselli/Repos/BillFolder/docs/superpowers/specs/2026-10-09-billfolder-web-admin-foundation-design.md`
- Modify: `/Users/jesstoselli/Repos/BillFolder/docs/superpowers/plans/2026-10-09-billfolder-web-admin-foundation.md`

**Interfaces:**
- Consumes: evidência de todas as tasks.
- Produces: documentação verdadeira, status de paridade final e números reproduzíveis.

- [ ] **Step 1: Executar verificação backend limpa**

Run:

```bash
cd /Users/jesstoselli/Repos/BillFolder
dotnet clean BillFolder.slnx
dotnet build BillFolder.slnx --no-incremental
dotnet test tests/BillFolder.Api.Tests/BillFolder.Api.Tests.csproj --no-build
```

Registrar totais, duração, warnings e errors reais.

- [ ] **Step 2: Executar gates web completos**

Run:

```bash
cd /Users/jesstoselli/Repos/BillFolderWeb
nvm use
npm run format:check
npm run audit:ci
npm run test:ci
npm run build:prod
npm run build:preview
npm run e2e
```

Registrar contagem real de testes unitários/estáticos/E2E, tamanho do bundle e auditoria. Não reutilizar números antigos.

- [ ] **Step 3: Atualizar documentação com os resultados finais**

No backlog, trocar:

```markdown
| Ciclos | pronta | entregue em 2026-10-09 |
| Contas-correntes | pronta | entregue em 2026-10-09 |
```

Manter Cartões, Poupança e Fontes de renda como infraestrutura pronta/UI administrativa pendente. Atualizar README/handoff com os novos números e registrar `account_in_use` como proteção cross-client.

- [ ] **Step 4: Marcar spec e plano como implementados somente se todos os gates estiverem verdes**

Alterar os status para:

```markdown
**Status:** implementado e verificado localmente; aguardando autorização de commit/push
```

Se algum gate falhar, manter status “em implementação” e documentar nominalmente a falha na entrega ao usuário.

- [ ] **Step 5: Rodar checks documentais finais**

Run:

```bash
cd /Users/jesstoselli/Repos/BillFolderWeb
npm run format:check
git diff --check
cd /Users/jesstoselli/Repos/BillFolder
git diff --check
```

Expected: PASS.

- [ ] **Step 6: Revisar arquitetura e segurança do diff completo**

Confirmar:

```text
- nenhum delete de conta em uso chega ao SaveChanges;
- nenhum write notifica em erro;
- referências de conta são invalidadas após sucesso;
- CycleStore não mantém ID removido;
- rotas continuam lazy e protegidas pelo authGuard;
- dialogs bloqueiam dismiss apenas durante writes;
- nenhuma credencial, token ou dado de produção entrou no diff.
```

- [ ] **Step 7: Checkpoint final sem commit/push**

Exibir `git status --short` e `git diff --stat` nos dois repositórios. Sugerir, sem executar:

```text
Backend: fix(accounts): protege exclusão de conta em uso
Web: feat(admin): gerencia ciclos e contas-correntes
Docs: docs: atualiza rollout e paridade administrativa
```

Solicitar autorização separada para commits e, depois, para qualquer push/deploy.
