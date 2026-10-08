using System.Buffers.Binary;
using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BillFolder.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();

    public async Task<IApplicationDbTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new ApplicationDbTransaction(transaction);
    }

    public Task AcquireRefreshTokenFamilyLockAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        Span<byte> bytes = stackalloc byte[16];
        familyId.TryWriteBytes(bytes);
        var lockKey = BinaryPrimitives.ReadInt64LittleEndian(bytes);

        return Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Carrega todas as IEntityTypeConfiguration<> do assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Mapeia ENUMs do Postgres
        // Os labels do Postgres são derivados automaticamente dos valores do enum C#
        // via NpgsqlSnakeCaseNameTranslator (default), e.g.:
        //   IncomeOriginType.Work       -> 'work'
        //   SavingsTransactionType.TransferOut -> 'transfer_out'
        //   IncomeStatus.NotOccurred    -> 'not_occurred'
        modelBuilder.HasPostgresEnum<IncomeOriginType>("public", "income_origin_type");
        modelBuilder.HasPostgresEnum<IncomeStatus>("public", "income_status");
        modelBuilder.HasPostgresEnum<ExpenseStatus>("public", "expense_status");
        modelBuilder.HasPostgresEnum<CardStatementStatus>("public", "card_statement_status");
        modelBuilder.HasPostgresEnum<SavingsTransactionType>("public", "savings_transaction_type");
        modelBuilder.HasPostgresEnum<CycleAdjustmentType>("public", "cycle_adjustment_type");
        modelBuilder.HasPostgresEnum<ExpenseRecurrenceFrequency>("public", "expense_recurrence_frequency");
    }

    private sealed class ApplicationDbTransaction(IDbContextTransaction transaction)
        : IApplicationDbTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return transaction.DisposeAsync();
        }
    }
}
