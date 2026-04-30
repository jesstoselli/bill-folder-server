using BillFolder.Domain.Enums;

namespace BillFolder.Application.Dtos.Home;

public sealed record HomeResponse(
    HomeCycleDto Cycle,
    HomeBalanceDto Balance,
    HomeIncomeBreakdownDto IncomeBreakdown,
    HomeExpenseBreakdownDto ExpenseBreakdown,
    IReadOnlyList<HomeUpcomingExpenseDto> UpcomingExpenses,
    IReadOnlyList<HomeCardStatementDto> CardStatementsInCycle,
    IReadOnlyList<HomeCategoryBreakdownDto> CategoryBreakdown);

/// <summary>
/// Soma agregada por categoria de tudo que passa pelo ciclo: despesas,
/// despesas avulsas e parcelas de fatura cujo vencimento cai no período.
/// Usado pelo pie chart "onde meu dinheiro vai" na Home.
/// Ordenado por valor desc — front pode pegar top-N pra renderizar.
/// </summary>
public sealed record HomeCategoryBreakdownDto(
    Guid CategoryId,
    string CategoryKey,
    string CategoryName,
    decimal Amount);

public sealed record HomeCycleDto(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    string Label);

public sealed record HomeBalanceDto(
    decimal CheckingAccountsTotal,
    decimal ExpectedIncome,
    decimal ReceivedIncome,
    decimal ExpectedExpenses,
    decimal PaidExpenses,
    decimal ExpectedCardStatements,
    decimal DailyExpensesSpent,
    decimal Remaining);

public sealed record HomeIncomeBreakdownDto(
    int Expected,
    int Received,
    int Late,
    int NotOccurred);

public sealed record HomeExpenseBreakdownDto(
    int Pending,
    int Overdue,
    int Paid);

public sealed record HomeUpcomingExpenseDto(
    Guid Id,
    string Label,
    DateOnly DueDate,
    decimal ExpectedAmount,
    ExpenseStatus Status,
    string CategoryName);

public sealed record HomeCardStatementDto(
    Guid Id,
    Guid CardId,
    string CardName,
    DateOnly DueDate,
    decimal TotalAmount,
    CardStatementStatus Status);
