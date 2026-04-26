namespace BillFolder.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public string? GoogleOauthId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string CycleStartRule { get; set; } = "5th_business_day";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public ICollection<CheckingAccount> CheckingAccounts { get; set; } = new List<CheckingAccount>();
    public ICollection<SavingsAccount> SavingsAccounts { get; set; } = new List<SavingsAccount>();
    public ICollection<CreditCardAccount> CreditCardAccounts { get; set; } = new List<CreditCardAccount>();
    public ICollection<Cycle> Cycles { get; set; } = new List<Cycle>();
    public ICollection<IncomeSource> IncomeSources { get; set; } = new List<IncomeSource>();
    public ICollection<IncomeEntry> IncomeEntries { get; set; } = new List<IncomeEntry>();
    public ICollection<ExpenseRecurrence> ExpenseRecurrences { get; set; } = new List<ExpenseRecurrence>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<DailyExpenseRecurrence> DailyExpenseRecurrences { get; set; } = new List<DailyExpenseRecurrence>();
    public ICollection<DailyExpense> DailyExpenses { get; set; } = new List<DailyExpense>();
    public ICollection<CardEntryRecurrence> CardEntryRecurrences { get; set; } = new List<CardEntryRecurrence>();
    public ICollection<CardEntry> CardEntries { get; set; } = new List<CardEntry>();
    public ICollection<CardStatement> CardStatements { get; set; } = new List<CardStatement>();
    public ICollection<SavingsTransaction> SavingsTransactions { get; set; } = new List<SavingsTransaction>();
    public ICollection<CycleAdjustment> CycleAdjustments { get; set; } = new List<CycleAdjustment>();
}
