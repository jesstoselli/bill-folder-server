using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.Abstractions.Persistence;

/// <summary>
/// Contrato pra Application acessar o DbContext sem depender da implementação
/// concreta (que vive em Infrastructure). Mantém Application desacoplada de EF Core
/// na medida do possível — só conhece DbSet&lt;T&gt; como abstração.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<CheckingAccount> CheckingAccounts { get; }
    DbSet<SavingsAccount> SavingsAccounts { get; }
    DbSet<CreditCardAccount> CreditCardAccounts { get; }
    DbSet<Cycle> Cycles { get; }
    DbSet<IncomeSource> IncomeSources { get; }
    DbSet<IncomeEntry> IncomeEntries { get; }
    DbSet<ExpenseRecurrence> ExpenseRecurrences { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<DailyExpenseRecurrence> DailyExpenseRecurrences { get; }
    DbSet<DailyExpense> DailyExpenses { get; }
    DbSet<CardEntryRecurrence> CardEntryRecurrences { get; }
    DbSet<CardEntry> CardEntries { get; }
    DbSet<CardStatement> CardStatements { get; }
    DbSet<Installment> Installments { get; }
    DbSet<SavingsTransaction> SavingsTransactions { get; }
    DbSet<CycleAdjustment> CycleAdjustments { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetRequest> PasswordResetRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
