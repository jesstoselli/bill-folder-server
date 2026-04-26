using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // DbSets — uma por entidade
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CheckingAccount> CheckingAccounts => Set<CheckingAccount>();
    public DbSet<SavingsAccount> SavingsAccounts => Set<SavingsAccount>();
    public DbSet<CreditCardAccount> CreditCardAccounts => Set<CreditCardAccount>();
    public DbSet<Cycle> Cycles => Set<Cycle>();
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<IncomeEntry> IncomeEntries => Set<IncomeEntry>();
    public DbSet<ExpenseRecurrence> ExpenseRecurrences => Set<ExpenseRecurrence>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<DailyExpenseRecurrence> DailyExpenseRecurrences => Set<DailyExpenseRecurrence>();
    public DbSet<DailyExpense> DailyExpenses => Set<DailyExpense>();
    public DbSet<CardEntryRecurrence> CardEntryRecurrences => Set<CardEntryRecurrence>();
    public DbSet<CardEntry> CardEntries => Set<CardEntry>();
    public DbSet<CardStatement> CardStatements => Set<CardStatement>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<SavingsTransaction> SavingsTransactions => Set<SavingsTransaction>();
    public DbSet<CycleAdjustment> CycleAdjustments => Set<CycleAdjustment>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Carrega todas as IEntityTypeConfiguration<> do assembly
        mb.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Mapeia ENUMs do Postgres
        // Os labels do Postgres são derivados automaticamente dos valores do enum C#
        // via NpgsqlSnakeCaseNameTranslator (default), e.g.:
        //   IncomeOriginType.Work       -> 'work'
        //   SavingsTransactionType.TransferOut -> 'transfer_out'
        //   IncomeStatus.NotOccurred    -> 'not_occurred'
        mb.HasPostgresEnum<IncomeOriginType>("public", "income_origin_type");
        mb.HasPostgresEnum<IncomeStatus>("public", "income_status");
        mb.HasPostgresEnum<ExpenseStatus>("public", "expense_status");
        mb.HasPostgresEnum<CardStatementStatus>("public", "card_statement_status");
        mb.HasPostgresEnum<SavingsTransactionType>("public", "savings_transaction_type");
        mb.HasPostgresEnum<CycleAdjustmentType>("public", "cycle_adjustment_type");
    }
}
