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

        var expectedExpenses = expenses
            .Where(e => e.Status != ExpenseStatus.Paid)
            .Sum(e => e.ExpectedAmount);

        var paidExpenses = expenses
            .Where(e => e.Status == ExpenseStatus.Paid)
            .Sum(e => e.ActualAmount ?? e.ExpectedAmount);

        var expenseBreakdown = new HomeExpenseBreakdownDto(
            Pending: expenses.Count(e => e.Status == ExpenseStatus.Pending
                                      && e.DueDate >= today),
            Overdue: expenses.Count(e => (e.Status == ExpenseStatus.Pending
                                        && e.DueDate < today)
                                     || e.Status == ExpenseStatus.Overdue),
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
                e.Category.NamePt))
            .ToList();

        // 5. Faturas que vencem no ciclo
        // Include CardEntry + Category pras installments porque vamos
        // agrupar por categoria no breakdown abaixo.
        var statements = await _db.CardStatements
            .AsNoTracking()
            .Where(s => s.UserId == userId
                     && s.DueDate >= cycle.StartDate
                     && s.DueDate <= cycle.EndDate)
            .Include(s => s.Card)
            .Include(s => s.Installments)
                .ThenInclude(i => i.CardEntry)
                    .ThenInclude(e => e.Category)
            .ToListAsync(ct);

        var expectedCardStatements = statements
            .Where(s => s.Status != CardStatementStatus.Paid)
            .Sum(s => s.Installments.Sum(i => i.Amount));

        var cardStatementsInCycle = statements
            .OrderBy(s => s.DueDate)
            .Select(s => new HomeCardStatementDto(
                s.Id,
                s.CardId,
                s.Card.Name,
                s.DueDate,
                s.Installments.Sum(i => i.Amount),
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

        // 7. Calcula remaining (o número grande do hero)
        var remaining = expectedIncome
                      - expectedExpenses
                      - expectedCardStatements
                      - dailyExpensesSpent;

        // 8. Breakdown por categoria (alimenta o pie chart "onde meu dinheiro vai")
        var categoryBreakdown = BuildCategoryBreakdown(expenses, dailyExpenses, statements);

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
    ///   herdam dela).
    /// </summary>
    private static List<HomeCategoryBreakdownDto> BuildCategoryBreakdown(
        List<Expense> expenses,
        List<DailyExpense> dailyExpenses,
        List<CardStatement> statements)
    {
        // Tupla: (categoryKey, namePt, amount). Agrupa primeiro pelo Id.
        var aggregator = new Dictionary<Guid, (string Key, string NamePt, decimal Sum)>();

        void Add(Category category, decimal amount)
        {
            if (aggregator.TryGetValue(category.Id, out var entry))
            {
                aggregator[category.Id] = (entry.Key, entry.NamePt, entry.Sum + amount);
            }
            else
            {
                aggregator[category.Id] = (category.Key, category.NamePt, amount);
            }
        }

        foreach (var e in expenses)
        {
            Add(e.Category, e.ExpectedAmount);
        }

        foreach (var d in dailyExpenses)
        {
            Add(d.Category, d.Amount);
        }

        foreach (var statement in statements)
        {
            foreach (var installment in statement.Installments)
            {
                Add(installment.CardEntry.Category, installment.Amount);
            }
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
