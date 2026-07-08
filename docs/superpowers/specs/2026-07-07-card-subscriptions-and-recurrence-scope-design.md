# Assinaturas de cartão + edição/exclusão com escopo — Design

**Data**: 2026-07-07
**Repos**: `BillFolder` (backend .NET) + `BillFolderApp` (Android)

## Context

Duas necessidades relacionadas surgiram depois do lote de recorrências:

1. **Cobranças recorrentes no cartão** (assinaturas digitais: Netflix, Spotify): uma cobrança mensal fixa que hoje precisa ser lançada à mão todo mês. A categoria "Assinaturas Digitais" já existe pra isso.
2. **Encerrar / editar uma recorrência**: a despesa semanal (já em produção) e a assinatura nova precisam poder ser **encerradas** (parei a terapia; cancelei o streaming) e a assinatura precisa poder ter o **valor reajustado daqui pra frente** (streaming aumentou). Hoje não há como encerrar uma recorrência — gap real da feature de despesa semanal.

O scaffold já existe e está dormente: entidade `CardEntryRecurrence` (CardId, DefaultLabel, DefaultAmount, DefaultCategoryId, `DayOfMonth`, Start/End, IsActive) + `CardEntry.TemplateId` + `CardEntryRecurrencesService` registrado — sem motor de geração. Mesmo padrão que ativamos para income e despesa semanal.

## Modelo unificado de ciclo de vida da recorrência

Vale para os dois tipos (despesa semanal existente + assinatura de cartão nova). Template (ativo/inativo) + ocorrências auto-geradas. Operações com **escopo** via modal:

- **"Só esta"** → mexe só na ocorrência selecionada.
- **"Esta e as próximas"** → mexe no **template + ocorrências futuras já materializadas** (mesmo template, data ≥ a desta, ainda não pagas/fechadas). **Passadas/fechadas ficam intactas.**

Geração: janela de ciclos (rolling window), consistente com income e despesa semanal — então "esta e as próximas" pode tocar até ~12 ocorrências futuras já geradas.

## Parte A — Assinatura de cartão (novo)

**Backend**
- Ativar `CardEntryRecurrence` (mensal, por `DayOfMonth`). Se faltar algo no schema, migration idempotente (a tabela já existe).
- Novo `CardEntryRecurrenceExpansion` (fork do `IncomeSourceExpansion`/`ProvisionedExpenseExpansion`): materializa **1 `CardEntry` à vista (`InstallmentsCount = 1`) por mês** com `PurchaseDate` = `DayOfMonth` no ciclo, `TemplateId` = recorrência. O `CardEntriesService`/`CardCycleCalculator` já posiciona na fatura certa. Idempotente por `(UserId, TemplateId, PurchaseDate)`.
- Wire: `CyclesService.CreateAsync` + `GenerateForwardCyclesAsync` (janela) e na criação do template.
- `CardEntryRecurrencesService`: método de criação + endpoints (`POST /v1/card-entry-recurrences` etc.).

**App**
- Toggle **"repetir todo mês"** no `AddCardEntrySheet`. Ligado: esconde o campo de parcelas, usa o dia da `purchaseDate` como `DayOfMonth`, e cria o template (a 1ª ocorrência vem da expansão) em vez de compra avulsa.

## Parte B — Edição/exclusão com escopo (backend + app)

**Backend** — param de escopo `scope: "this" | "this_and_following"`:
- **Excluir** (despesa semanal E assinatura de cartão):
  - `this` → apaga só a ocorrência (delete atual).
  - `this_and_following` → `IsActive = false` no template + apaga esta e as futuras não-fechadas do mesmo template.
- **Editar valor** (só assinatura de cartão no v1): habilitar edição de valor para lançamento `InstallmentsCount = 1` (recalcular a parcela única).
  - `this` → só esta `CardEntry`.
  - `this_and_following` → atualiza `DefaultAmount` do template + valor das ocorrências futuras materializadas.

**App**
- Modal de escopo ("só esta" / "esta e as próximas") no fluxo de excluir (swipe/tap) das duas recorrências, e no editar-valor da assinatura.

## Semântica de "as próximas"

Ocorrências do mesmo `TemplateId` com data (DueDate / PurchaseDate→statement) ≥ a desta e ainda não pagas/fechadas. Passadas nunca mudam. Encerrar sempre desativa o template (`IsActive = false`) para parar a geração futura.

## Fora do escopo (v2 / backlog)

- **Editar-valor-com-escopo da despesa semanal** (mesmo mecanismo do cartão; adiado a pedido).
- **Bug relacionado**: editar o valor total da despesa semanal não recalcula o valor por sessão (`OccurrenceAmount` imutável). Resolver junto do v2 do edit-scope.

## Testes + verificação

- Backend: unit tests do `CardEntryRecurrenceExpansion` (dia do mês, virada de mês, fatura correta), das operações com escopo (this vs following, passado intacto, template desativado no "following"), e do recalc da parcela única no edit de valor. `dotnet test` + build limpo (`TreatWarningsAsErrors`).
- App: VMs (toggle de recorrência no AddCardEntry, modal de escopo) + repos dão bump no bus. `gradlew testDebugUnitTest` + `assembleDebug`.
- E2E no device: criar assinatura → aparece todo mês na fatura certa → reajustar valor (esta e as próximas) → encerrar (esta e as próximas). Encerrar despesa semanal via o mesmo modal.
- Deploy: se houver mudança de schema, migration idempotente aplicada **antes** do deploy do backend (e restart do container se adicionar enum — lição do lote anterior).
