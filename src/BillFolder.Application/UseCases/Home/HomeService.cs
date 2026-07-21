using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Home;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Home;

public class HomeService
{
    private readonly IApplicationDbContext _db;

    public HomeService(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Constrói o payload da home screen pra o ciclo informado (ou o ciclo atual se omitido).
    /// Faz 6 queries paralelas-ish — pode otimizar mais tarde se virar gargalo.
    /// </summary>
    public async Task<OperationResult<HomeResponse>> GetHomeAsync(
        Guid userId,
        Guid? cycleId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        // 1. Achar o ciclo
        var cycle = cycleId.HasValue
            ? await _db.Cycles
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cycleId.Value && c.UserId == userId, ct)
            : await _db.Cycles
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId
                                       && c.StartDate <= today
                                       && c.EndDate >= today, ct);

        if (cycle is null)
        {
            return OperationResult.Fail<HomeResponse>(
                "no_cycle",
                cycleId.HasValue
                    ? "Ciclo não encontrado."
                    : "Nenhum ciclo ativo cobre a data de hoje.");
        }

        // 2. Total das contas correntes (initial_balance — saldo de partida do usuário)
        var checkingTotal = await _db.CheckingAccounts
            .Where(a => a.UserId == userId)
            .SumAsync(a => (decimal?)a.InitialBalance, ct) ?? 0m;

        // 3. Receitas no ciclo
        var incomeEntries = await _db.IncomeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId
                     && e.ExpectedDate >= cycle.StartDate
                     && e.ExpectedDate <= cycle.EndDate)
            .ToListAsync(ct);

        var expectedIncome = incomeEntries
            .Where(e => e.Status != IncomeStatus.NotOccurred)
            .Sum(e => e.ExpectedAmount);

        var receivedIncome = incomeEntries
            .Where(e => e.Status == IncomeStatus.Received)
            .Sum(e => e.ActualAmount ?? 0m);

        var incomeBreakdown = new HomeIncomeBreakdownDto(
            Expected: incomeEntries.Count(e => e.Status == IncomeStatus.Expected
                                            && e.ExpectedDate >= today),
            Received: incomeEntries.Count(e => e.Status == IncomeStatus.Received),
            Late: incomeEntries.Count(e => (e.Status == IncomeStatus.Expected
                                          && e.ExpectedDate < today)
                                       || e.Status == IncomeStatus.Late),
            NotOccurred: incomeEntries.Count(e => e.Status == IncomeStatus.NotOccurred));

        // 4. Despesas no ciclo (com Category pra denormalizar nome)
        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId
                     && e.DueDate >= cycle.StartDate
                     && e.DueDate <= cycle.EndDate)
            .Include(e => e.Category)
            .ToListAsync(ct);

        var (expectedExpenses, paidExpenses) = ComputeExpenseBuckets(expenses);

        // Breakdown: provisionada em andamento conta como Pending (nunca Overdue),
        // consistente com ComputeDisplayStatus.
        var expenseBreakdown = new HomeExpenseBreakdownDto(
            Pending: expenses.Count(e => e.Status == ExpenseStatus.Pending
                                      && (e.OccurrencesTotal != null || e.DueDate >= today)),
            Overdue: expenses.Count(e => e.OccurrencesTotal == null
                                     && ((e.Status == ExpenseStatus.Pending && e.DueDate < today)
                                         || e.Status == ExpenseStatus.Overdue)),
            Paid: expenses.Count(e => e.Status == ExpenseStatus.Paid));

        var upcomingExpenses = expenses
            .Where(e => e.Status != ExpenseStatus.Paid)
            .OrderBy(e => e.DueDate)
            .Take(5)
            .Select(e => new HomeUpcomingExpenseDto(
                e.Id,
                e.Label,
                e.DueDate,
                e.ExpectedAmount,
                e.ComputeDisplayStatus(today),
                e.Category.NamePt,
                e.OccurrencesTotal,
                e.OccurrencesPaid,
                e.PaidToDate))
            .ToList();

        // 5. Faturas que vencem no ciclo
        //
        // Projetamos TUDO em SQL — inclusive Total e o breakdown por
        // categoria de cada installment — em vez de fazer Include+Sum
        // in-memory. Motivo: `.Include(collection)` + `.AsNoTracking()`
        // em EF Core pode não popular a collection corretamente sob
        // certos cenários (cartesian explosion sem identity resolution
        // com múltiplos ThenInclude), fazendo `s.Installments.Sum(...)`
        // retornar 0 mesmo com parcelas existindo no DB. A projeção
        // server-side abaixo é imune a esse quirk — SUM/JOIN são
        // computados pelo Postgres.
        var statementProjections = await _db.CardStatements
            .AsNoTracking()
            .Where(s => s.UserId == userId
                     && s.DueDate >= cycle.StartDate
                     && s.DueDate <= cycle.EndDate)
            .OrderBy(s => s.DueDate)
            .Select(s => new
            {
                s.Id,
                s.CardId,
                CardName = s.Card.Name,
                s.DueDate,
                s.Status,
                // SUM(amount) executado no Postgres, retorna decimal.
                // Nullable pra cobrir statement sem installments (evita
                // "sequence contains no elements").
                Total = s.Installments.Sum(i => (decimal?)i.Amount) ?? 0m,
                // Breakdown por categoria — flat map de (amount, category)
                // pra alimentar BuildCategoryBreakdown sem precisar dos
                // objetos completos de Installment/CardEntry/Category.
                Categories = s.Installments
                    .Select(i => new HomeStatementCategoryProjection(
                        i.Amount,
                        i.CardEntry.CategoryId,
                        i.CardEntry.Category.Key,
                        i.CardEntry.Category.NamePt))
                    .ToList(),
            })
            .ToListAsync(ct);

        var expectedCardStatements = statementProjections
            .Where(s => s.Status != CardStatementStatus.Paid)
            .Sum(s => s.Total);

        var cardStatementsInCycle = statementProjections
            .Select(s => new HomeCardStatementDto(
                s.Id,
                s.CardId,
                s.CardName,
                s.DueDate,
                s.Total,
                s.Status))
            .ToList();

        // 6. Daily expenses no ciclo
        // Carrega a lista (não só a soma) pra reusar no breakdown por categoria.
        var dailyExpenses = await _db.DailyExpenses
            .AsNoTracking()
            .Where(d => d.UserId == userId
                     && d.Date >= cycle.StartDate
                     && d.Date <= cycle.EndDate)
            .Include(d => d.Category)
            .ToListAsync(ct);

        var dailyExpensesSpent = dailyExpenses.Sum(d => d.Amount);

        // 6b. Cycle adjustments (ajustes avulsos do ciclo — vendas, estornos,
        // saques da poupança, presentes eventuais). Inflows aumentam
        // remaining, outflows diminuem.
        var adjustments = await _db.CycleAdjustments
            .AsNoTracking()
            .Where(a => a.UserId == userId
                     && a.Date >= cycle.StartDate
                     && a.Date <= cycle.EndDate)
            .Select(a => new { a.Type, a.Amount })
            .ToListAsync(ct);

        var adjustmentsInflows = adjustments
            .Where(a => a.Type == CycleAdjustmentType.Inflow)
            .Sum(a => a.Amount);
        var adjustmentsOutflows = adjustments
            .Where(a => a.Type == CycleAdjustmentType.Outflow)
            .Sum(a => a.Amount);

        // 7. Calcula remaining (o número grande do hero)
        //
        // Semântica: "quanto de saldo resta ao fim do ciclo, considerando
        // tudo que já rolou e tudo que ainda tem que acontecer". Fórmula:
        //
        //   remaining = saldo de partida das contas
        //             + tudo que entra no ciclo (receitas esperadas + recebidas + em atraso)
        //             - tudo que sai no ciclo (despesas pagas + pendentes + faturas + avulsas)
        //
        // Bugs anteriores da fórmula:
        //   - Não incluía checkingTotal → dava negativo mesmo com dinheiro na conta.
        //   - Só somava expectedExpenses (filtra Status != Paid) e ignorava
        //     paidExpenses — expense paga "sumia" do cálculo, mesmo tendo
        //     saído da conta na vida real.
        //   - expectedCardStatements filtra faturas não-pagas — faturas
        //     pagas sumiam pelo mesmo motivo. Aqui somamos TODAS as
        //     installments do ciclo (pagas ou não).
        var totalCardCharges = statementProjections.Sum(s => s.Total);

        var remaining = checkingTotal
                      + expectedIncome
                      + adjustmentsInflows
                      - paidExpenses
                      - expectedExpenses
                      - totalCardCharges
                      - dailyExpensesSpent
                      - adjustmentsOutflows;

        // 8. Breakdown por categoria (alimenta o pie chart "onde meu dinheiro vai")
        var cardCategorySlices = statementProjections
            .SelectMany(s => s.Categories)
            .ToList();
        var categoryBreakdown = BuildCategoryBreakdown(
            expenses, dailyExpenses, cardCategorySlices, adjustmentsOutflows);

        var response = new HomeResponse(
            Cycle: new HomeCycleDto(cycle.Id, cycle.StartDate, cycle.EndDate, cycle.Label),
            Balance: new HomeBalanceDto(
                CheckingAccountsTotal: checkingTotal,
                ExpectedIncome: expectedIncome,
                ReceivedIncome: receivedIncome,
                ExpectedExpenses: expectedExpenses,
                PaidExpenses: paidExpenses,
                ExpectedCardStatements: expectedCardStatements,
                DailyExpensesSpent: dailyExpensesSpent,
                Remaining: remaining),
            IncomeBreakdown: incomeBreakdown,
            ExpenseBreakdown: expenseBreakdown,
            UpcomingExpenses: upcomingExpenses,
            CardStatementsInCycle: cardStatementsInCycle,
            CategoryBreakdown: categoryBreakdown);

        return OperationResult.Ok(response);
    }

    /// <summary>
    /// Agrega despesas + daily expenses + parcelas de fatura por categoria.
    /// Retorna lista ordenada por valor desc.
    ///
    /// Convenção:
    /// - Despesas tradicionais somam pelo `ExpectedAmount` (independente de
    ///   estado pago/pending) — o ciclo é "olhada do mês inteiro", a categoria
    ///   já tá comprometida.
    /// - Daily expenses somam `Amount` (já é o real).
    /// - Installments somam pelo valor da parcela; a categoria vem do
    ///   CardEntry pai (cada CardEntry tem 1 categoria, todas as parcelas
    ///   herdam dela). Recebemos essa fatia já projetada em SQL (com
    ///   categoryId/key/name flat), evitando depender de Include+navigation.
    /// - Cycle adjustments não têm categoria real. Os OUTFLOWS (só eles —
    ///   inflows são crédito e não cabem num gráfico de gastos) entram como
    ///   uma única fatia sintética "Ajustes", com CategoryId = Guid.Empty
    ///   (sentinela que nunca colide com categoria real e serializa como
    ///   string não-nula pro app). Se a soma de outflows for zero, a fatia
    ///   não é adicionada.
    /// </summary>
    /// <summary>
    /// Divide as despesas do ciclo em (Reserved, Realized):
    ///  - Reserved: despesa normal não-paga reserva o ExpectedAmount cheio; uma
    ///    provisionada em andamento reserva só o que falta (ExpectedAmount − PaidToDate).
    ///  - Realized: despesas quitadas (ActualAmount ?? ExpectedAmount) + o
    ///    PaidToDate das provisionadas ainda em andamento.
    /// Invariante: por despesa provisionada, Reserved + Realized = ExpectedAmount
    /// enquanto não quitada (protege o mês cheio sem contar em dobro o já-pago).
    /// </summary>
    internal static (decimal Reserved, decimal Realized) ComputeExpenseBuckets(
        IReadOnlyCollection<Expense> expenses)
    {
        ArgumentNullException.ThrowIfNull(expenses);

        var reserved = expenses
            .Where(e => e.Status != ExpenseStatus.Paid)
            .Sum(e => e.OccurrencesTotal != null
                ? e.ExpectedAmount - e.PaidToDate
                : e.ExpectedAmount);

        var realized = expenses
            .Where(e => e.Status == ExpenseStatus.Paid)
            .Sum(e => e.ActualAmount ?? e.ExpectedAmount)
          + expenses
            .Where(e => e.Status != ExpenseStatus.Paid && e.OccurrencesTotal != null)
            .Sum(e => e.PaidToDate);

        return (reserved, realized);
    }

    internal static List<HomeCategoryBreakdownDto> BuildCategoryBreakdown(
        List<Expense> expenses,
        List<DailyExpense> dailyExpenses,
        List<HomeStatementCategoryProjection> cardSlices,
        decimal adjustmentsOutflows)
    {
        // Tupla: (categoryKey, namePt, amount). Agrupa primeiro pelo Id.
        var aggregator = new Dictionary<Guid, (string Key, string NamePt, decimal Sum)>();

        void Add(Guid categoryId, string categoryKey, string categoryName, decimal amount)
        {
            if (aggregator.TryGetValue(categoryId, out var entry))
            {
                aggregator[categoryId] = (entry.Key, entry.NamePt, entry.Sum + amount);
            }
            else
            {
                aggregator[categoryId] = (categoryKey, categoryName, amount);
            }
        }

        foreach (var e in expenses)
        {
            Add(e.Category.Id, e.Category.Key, e.Category.NamePt, e.ExpectedAmount);
        }

        foreach (var d in dailyExpenses)
        {
            Add(d.Category.Id, d.Category.Key, d.Category.NamePt, d.Amount);
        }

        foreach (var slice in cardSlices)
        {
            Add(slice.CategoryId, slice.CategoryKey, slice.CategoryName, slice.Amount);
        }

        // Fatia sintética "Ajustes" pros outflows de cycle adjustments.
        // Só entra se houver outflow (soma > 0); inflows nunca aparecem aqui.
        // Guid.Empty é sentinela — não colide com categoria real e serializa
        // como "00000000-..." (string não-nula) pro DTO do app.
        if (adjustmentsOutflows > 0m)
        {
            Add(Guid.Empty, "ajustes", "Ajustes", adjustmentsOutflows);
        }

        return aggregator
            .Select(kv => new HomeCategoryBreakdownDto(
                CategoryId: kv.Key,
                CategoryKey: kv.Value.Key,
                CategoryName: kv.Value.NamePt,
                Amount: kv.Value.Sum))
            .OrderByDescending(c => c.Amount)
            .ToList();
    }
}

/// <summary>
/// Fatia (installment amount + categoria do CardEntry pai) projetada no SQL.
/// Usado pelo BuildCategoryBreakdown pra não depender de Include/navigation
/// nas installments — a projeção server-side é imune ao bug de
/// Include+AsNoTracking com collections aninhadas.
/// </summary>
internal sealed record HomeStatementCategoryProjection(
    decimal Amount,
    Guid CategoryId,
    string CategoryKey,
    string CategoryName);
