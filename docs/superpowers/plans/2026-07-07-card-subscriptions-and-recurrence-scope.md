# Assinaturas de cartão + edição/exclusão com escopo — Plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Cobranças recorrentes mensais no cartão (assinaturas) que auto-aparecem na fatura certa, + encerrar/editar recorrências com escopo ("só esta" vs "esta e as próximas") no cartão e na despesa semanal.

**Architecture:** Reusa o padrão de expansão já existente (`ProvisionedExpenseExpansion`/`IncomeSourceExpansion`) para materializar 1 `CardEntry` à vista por mês a partir do `CardEntryRecurrence` (dormente). Operações com escopo via param `scope` no backend + modal no app. Passadas/fechadas nunca mudam; "esta e as próximas" desativa o template e mexe nas ocorrências futuras materializadas.

**Tech Stack:** .NET 10 + EF Core + PostgreSQL (backend); Kotlin/Compose + Hilt + Retrofit (app). Testes: xUnit (backend), JUnit + FakeBillFolderApi/MainDispatcherRule (app).

**Referências concretas no repo (espelhar):**
- `src/BillFolder.Application/UseCases/Recurrences/ProvisionedExpenseExpansion.cs` — template do motor de expansão + helper de datas.
- `src/BillFolder.Application/UseCases/Cards/CardEntriesService.cs` (CreateAsync ~83-208) — como um CardEntry vira installments numa statement (reusar pra a 1ª ocorrência).
- `src/BillFolder.Application/UseCases/Expenses/ExpensesService.cs` (`PayOccurrenceAsync`, `ComputeExpenseBuckets`) — padrão de service + teste via helper interno.
- `tests/BillFolder.Api.Tests/Recurrences/WeekdayDatesInRangeTests.cs` e `Home/ExpenseBucketsTests.cs` — estilo de teste puro.

---

## Fase A — Backend: motor de expansão da assinatura

### Task A1: Helper de datas mensais + expansão de CardEntryRecurrence

**Files:**
- Create: `src/BillFolder.Application/UseCases/Recurrences/CardEntryRecurrenceExpansion.cs`
- Test: `tests/BillFolder.Api.Tests/Recurrences/MonthlyDateInCycleTests.cs`

- [ ] **Step 1 — Teste falho do helper de data** (espelha `WeekdayDatesInRangeTests`). O helper `MonthlyDateInRange(start, end, dayOfMonth)` retorna a data do `dayOfMonth` dentro de `[start,end]` (com clamp de mês curto: dia 31 em fev → 28/29), ou `null` se o dia não cai no range.

```csharp
using BillFolder.Application.UseCases.Recurrences;
namespace BillFolder.Api.Tests.Recurrences;

public class MonthlyDateInCycleTests
{
    [Fact]
    public void Returns_the_day_of_month_inside_the_range()
        => Assert.Equal(new DateOnly(2026, 7, 15),
            CardEntryRecurrenceExpansion.MonthlyDateInRange(new(2026,7,1), new(2026,7,31), 15));

    [Fact]
    public void Clamps_day_31_to_last_day_of_february()
        => Assert.Equal(new DateOnly(2026, 2, 28),
            CardEntryRecurrenceExpansion.MonthlyDateInRange(new(2026,2,1), new(2026,2,28), 31));

    [Fact]
    public void Returns_null_when_the_day_falls_outside_a_partial_range()
        => Assert.Null(CardEntryRecurrenceExpansion.MonthlyDateInRange(new(2026,7,20), new(2026,7,31), 5));
}
```

- [ ] **Step 2 — Rodar e ver falhar:** `dotnet test --filter "FullyQualifiedName~MonthlyDateInCycleTests"` → FAIL (classe não existe).

- [ ] **Step 3 — Implementar a classe + helper + expansão.** Espelhar `ProvisionedExpenseExpansion` (mesma estrutura `ExpandForTemplateAsync` / `ExpandForCycleAsync` / `MaterializeAsync`), com estas diferenças:
  - Filtra `CardEntryRecurrence` (não Expense) — não há `Frequency` (o template já é mensal por `DayOfMonth`); condição = `IsActive && StartDate <= cycle.End && (EndDate == null || EndDate >= cycle.Start)`.
  - `MaterializeAsync`: calcula `purchaseDate = MonthlyDateInRange(effectiveStart, effectiveEnd, rec.DayOfMonth)`; se `null`, return. Idempotência por `(UserId, TemplateId, PurchaseDate)` em `_db.CardEntries`. Cria a compra reusando a MESMA lógica de statement do `CardEntriesService.CreateAsync` — extrair essa lógica num método compartilhado `CardEntriesService.MaterializeSingleChargeAsync(db, userId, cardId, purchaseDate, label, amount, categoryId, templateId, ct)` (installmentsCount=1) e chamá-lo aqui, pra não duplicar o cálculo de fatura.

```csharp
internal static DateOnly? MonthlyDateInRange(DateOnly start, DateOnly end, int dayOfMonth)
{
    if (start > end) return null;
    var days = DateTime.DaysInMonth(start.Year, start.Month);
    var day = Math.Min(dayOfMonth, days);
    var candidate = new DateOnly(start.Year, start.Month, day);
    if (candidate >= start && candidate <= end) return candidate;
    // tenta o mês de `end` se o range cruza virada de mês (ciclos BillFolder são ~mensais)
    if (end.Month != start.Month)
    {
        var days2 = DateTime.DaysInMonth(end.Year, end.Month);
        var c2 = new DateOnly(end.Year, end.Month, Math.Min(dayOfMonth, days2));
        if (c2 >= start && c2 <= end) return c2;
    }
    return null;
}
```

- [ ] **Step 4 — Rodar e ver passar.** `dotnet test --filter "FullyQualifiedName~MonthlyDateInCycleTests"` → PASS.
- [ ] **Step 5 — Commit.** `feat(cards): CardEntryRecurrenceExpansion + helper de data mensal`

### Task A2: Extrair materialização de compra única no CardEntriesService

**Files:**
- Modify: `src/BillFolder.Application/UseCases/Cards/CardEntriesService.cs`

- [ ] **Step 1** — Refatorar `CreateAsync`: extrair o miolo que cria o `CardEntry` + installments + find-or-create statement num método reutilizável (público estático ou de instância) que recebe `(db, userId, cardId, purchaseDate, label, totalAmount, installmentsCount, categoryId, templateId?, notes?)`. `CreateAsync` passa a chamá-lo com `installmentsCount` do request; a expansão (A1) chama com `installmentsCount=1` e `templateId`. Sem mudança de comportamento pro caso avulso.
- [ ] **Step 2** — `dotnet build` limpo (sem warnings; `TreatWarningsAsErrors`).
- [ ] **Step 3 — Commit.** `refactor(cards): materialização de compra única reutilizável`

### Task A3: Wire da expansão (ciclo + criação de template)

**Files:**
- Modify: `src/BillFolder.Application/UseCases/Cycles/CyclesService.cs` (após os 2 `ProvisionedExpenseExpansion.ExpandForCycleAsync` — CreateAsync ~linha 178 e GenerateForwardCyclesAsync)
- Modify: `src/BillFolder.Application/UseCases/Recurrences/CardEntryRecurrencesService.cs` (CreateAsync: após salvar, chamar `CardEntryRecurrenceExpansion.ExpandForTemplateAsync` + SaveChanges — espelhar o que `ExpenseRecurrencesService.CreateAsync` faz)

- [ ] **Step 1** — Adicionar `await CardEntryRecurrenceExpansion.ExpandForCycleAsync(_db, cycle, ct);` nos 2 pontos do CyclesService (mesmo padrão do income/despesa).
- [ ] **Step 2** — No `CardEntryRecurrencesService.CreateAsync`, após `SaveChangesAsync`: `await CardEntryRecurrenceExpansion.ExpandForTemplateAsync(_db, rec, ct); await _db.SaveChangesAsync(ct);`
- [ ] **Step 3** — `dotnet build` limpo.
- [ ] **Step 4 — Commit.** `feat(cards): auto-gera assinatura por ciclo`

---

## Fase B — Backend: operações com escopo

### Task B1: Enum de escopo + delete-com-escopo da despesa semanal

**Files:**
- Create: `src/BillFolder.Domain/Enums/RecurrenceScope.cs` (`This`, `ThisAndFollowing`)
- Modify: `src/BillFolder.Application/UseCases/Expenses/ExpensesService.cs` (novo `DeleteAsync(userId, id, RecurrenceScope scope, ct)` — ou overload)
- Modify: `src/BillFolder.Api/Endpoints/ExpensesEndpoints.cs` (DELETE aceita `?scope=`)
- Test: `tests/BillFolder.Api.Tests/Expenses/RecurrenceScopeDeleteTests.cs`

- [ ] **Step 1 — Teste falho de um helper puro de seleção.** Extrair a decisão "quais ocorrências apagar" num método interno testável `ExpensesService.OccurrencesToDelete(all, target, scope)` que, dado a lista de despesas do mesmo `TemplateId`, a alvo, e o scope, retorna os ids a apagar: `This` → só a alvo; `ThisAndFollowing` → alvo + as de `DueDate >= target.DueDate` que **não** estão `Paid`.

```csharp
// dado 3 despesas do template (jun/jul/ago, jul = alvo), ThisAndFollowing → {jul, ago}; jun fica.
```
(Escrever o teste concreto com 3 `Expense` in-memory, espelhando `ExpenseBucketsTests`.)

- [ ] **Step 2** — Rodar → FAIL.
- [ ] **Step 3 — Implementar** `OccurrencesToDelete` + o `DeleteAsync` com scope: se a despesa tem `TemplateId` e scope=`ThisAndFollowing`, desativa o template (`ExpenseRecurrence.IsActive=false`) e apaga o conjunto; senão apaga só a alvo. Endpoint: `DELETE /v1/expenses/{id}?scope=this|this_and_following` (parse case-insensitive, default `this`).
- [ ] **Step 4** — Rodar → PASS. `dotnet build` limpo.
- [ ] **Step 5 — Commit.** `feat(expenses): excluir despesa semanal com escopo`

### Task B2: Delete-com-escopo da assinatura de cartão

**Files:**
- Modify: `src/BillFolder.Application/UseCases/Cards/CardEntriesService.cs` (delete com scope; "following" = mesmas `CardEntry` do template com `PurchaseDate >= target` cuja statement **não está fechada/paga** — checar `CardStatement.Status`)
- Modify: `src/BillFolder.Api/Endpoints/` (endpoint de delete de card entry aceita `?scope=`)
- Test: `tests/BillFolder.Api.Tests/Cards/CardSubscriptionScopeTests.cs`

- [ ] **Step 1 — Teste falho** do helper puro `CardEntriesService.SubscriptionOccurrencesToDelete(all, target, scope, statementStatusById)` (só toca as não-fechadas quando ThisAndFollowing).
- [ ] **Step 2** — Rodar → FAIL.
- [ ] **Step 3 — Implementar** helper + delete com scope (desativa `CardEntryRecurrence` no ThisAndFollowing). Deletar CardEntry já cascateia installments (comportamento existente).
- [ ] **Step 4** — Rodar → PASS. Build limpo.
- [ ] **Step 5 — Commit.** `feat(cards): excluir assinatura com escopo`

### Task B3: Editar valor da assinatura com escopo

**Files:**
- Modify: `src/BillFolder.Application/Dtos/Cards/*` (novo `UpdateCardSubscriptionAmountRequest { decimal Amount; RecurrenceScope Scope; }`)
- Modify: `src/BillFolder.Application/UseCases/Cards/CardEntriesService.cs` (editar valor de entry 1×: recalcular a installment única; scope `ThisAndFollowing` também atualiza `CardEntryRecurrence.DefaultAmount` + as ocorrências futuras não-fechadas)
- Modify: endpoint `POST /v1/card-entries/{id}/update-amount` (com scope)
- Test: `tests/BillFolder.Api.Tests/Cards/CardSubscriptionAmountEditTests.cs`

- [ ] **Step 1 — Teste falho** do helper puro que, dado a lista de ocorrências + alvo + scope + statusById, decide quais ids recebem o novo valor (This → só alvo; ThisAndFollowing → alvo + futuras não-fechadas) — e que o template é atualizado no ThisAndFollowing.
- [ ] **Step 2** — Rodar → FAIL.
- [ ] **Step 3 — Implementar.** Guardar: só permite pra entry `InstallmentsCount==1`; recalcula a `Installment` única = novo valor. Endpoint + validator (`Amount > 0`).
- [ ] **Step 4** — Rodar → PASS. Build limpo. `dotnet test` (suite inteira) verde.
- [ ] **Step 5 — Commit.** `feat(cards): reajustar valor da assinatura com escopo`

---

## Fase C — App

### Task C1: DTOs + repositório (com bump no bus)

**Files:**
- Modify: `app/.../data/dto/CardDtos.kt` (DTOs de `CardEntryRecurrence` create; `UpdateCardSubscriptionAmountRequest`; enum de scope como String `"this"|"this_and_following"`)
- Modify: `app/.../data/api/BillFolderApi.kt` (createCardEntryRecurrence; deleteCardEntry com `@Query scope`; deleteExpense com `@Query scope`; updateCardSubscriptionAmount)
- Modify: `app/.../data/repository/CardsRepository.kt` e `ExpensesRepository.kt` (novos writes dentro de `notifier.notifyingOnSuccess`)
- Test: estender `RepositoryNotifyTest` (cada novo write dá bump)

- [ ] Steps TDD: (1) adicionar casos no `RepositoryNotifyTest` pros novos writes → FAIL; (2) implementar DTOs/api/repo; (3) tests verdes; (4) commit `feat(cards): dados/repo de assinatura + scope`.

### Task C2: Toggle "repetir todo mês" no AddCardEntrySheet

**Files:**
- Modify: `app/.../ui/screens/cards/AddCardEntryViewModel.kt` (+ campo `repeatMonthly: Boolean`; quando true: força installments=1, deriva `dayOfMonth` da purchaseDate, submit chama `createCardEntryRecurrence`)
- Modify: `app/.../ui/screens/cards/AddCardEntrySheet.kt` (switch "repetir todo mês"; esconde campo de parcelas quando ligado)
- Test: `AddCardEntryViewModelTest` — submit com repeat=true chama createCardEntryRecurrence com dayOfMonth correto; repeat=false mantém o fluxo atual.

- [ ] Steps TDD RED→GREEN→commit `feat(cards): toggle repetir todo mês`.

### Task C3: Modal de escopo + wiring de excluir/editar

**Files:**
- Create: `app/.../ui/components/RecurrenceScopeDialog.kt` (AlertDialog: "Só esta" / "Esta e as próximas" / cancelar → callback com o scope)
- Modify: `ExpensesScreen`/`ExpensesViewModel` — ao excluir uma despesa provisionada (`isProvisioned()`), abre o modal de escopo antes de deletar; despesa normal segue direto.
- Modify: `CardsScreen`/`CardsViewModel` — excluir uma assinatura (entry com templateId) abre o modal; editar valor de assinatura abre modal + sheet de valor.
- Test: helper puro de "precisa perguntar escopo?" (`ExpenseResponse.isProvisioned()` já existe; criar equivalente pra card entry: `CardEntryResponse.isSubscription()` = `templateId != null`) + testar a decisão.

- [ ] Steps TDD: (1) helper `isSubscription()` + teste → FAIL; (2) implementar helper + modal + wiring; (3) testes verdes; (4) commit `feat(recurrence): modal de escopo pra excluir/editar`.

### Task C4: Build + suíte

- [ ] `./gradlew :app:testDebugUnitTest --rerun-tasks` verde.
- [ ] `./gradlew :app:assembleDebug` verde.
- [ ] Commit final se houver ajustes.

---

## Fase D — Deploy + E2E

- [ ] Se A/B adicionaram coluna/enum ao schema: escrever migration idempotente em `db/migrations/` + atualizar `db/schema.sql`. (Provável: nenhuma — `CardEntryRecurrence` já tem tabela; scope é param, não coluna.) Se adicionar enum Postgres, **rodar migration antes do deploy e reiniciar o container** (lição do lote anterior — Npgsql cacheia tipos no startup).
- [ ] Push backend → CI/CD; validar logs do container.
- [ ] `./gradlew :app:installDebug`.
- [ ] E2E: criar assinatura (toggle) → aparece na fatura certa do mês → navegar meses e ver repetir → reajustar valor "esta e as próximas" → encerrar "esta e as próximas". Encerrar despesa semanal via o mesmo modal.

## Self-review (cobertura do spec)
- Assinatura mensal auto-gerada → A1–A3. ✓
- Toggle no AddCardEntry (opção B) → C2. ✓
- Excluir com escopo (despesa semanal + cartão) → B1, B2, C3. ✓
- Editar valor com escopo (só cartão v1) → B3, C3. ✓
- "As próximas" = futuras não-fechadas, passado intacto, template desativado → B1/B2/B3 helpers. ✓
- v2 (edit-scope despesa semanal) e bug do recalc por sessão → fora do escopo (memória + spec). ✓
