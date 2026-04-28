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
                ComputeExpenseDisplayStatus(e, today),
                e.Category.NamePt))
            .ToList();

        // 5. Faturas que vencem no ciclo
        var statements = await _db.CardStatements
            .AsNoTracking()
            .Where(s => s.UserId == userId
                     && s.DueDate >= cycle.StartDate
                     && s.DueDate <= cycle.EndDate)
            .Include(s => s.Card)
            .Include(s => s.Installments)
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
        var dailyExpensesSpent = await _db.DailyExpenses
            .Where(d => d.UserId == userId
                     && d.Date >= cycle.StartDate
                     && d.Date <= cycle.EndDate)
            .SumAsync(d => (decimal?)d.Amount, ct) ?? 0m;

        // 7. Calcula remaining (o número grande do hero)
        var remaining = expectedIncome
                      - expectedExpenses
                      - expectedCardStatements
                      - dailyExpensesSpent;

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
            CardStatementsInCycle: cardStatementsInCycle);

        return OperationResult.Ok(response);
    }

    private static ExpenseStatus ComputeExpenseDisplayStatus(Expense e, DateOnly today) =>
        e.Status == ExpenseStatus.Pending && e.DueDate < today
            ? ExpenseStatus.Overdue
            : e.Status;
}
