namespace BillFolder.Domain.Entities;

public class CheckingAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BankName { get; set; } = null!;
    public string? Branch { get; set; }
    public string? AccountNumber { get; set; }
    public decimal InitialBalance { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public SavingsAccount? SavingsAccount { get; set; } // 1:0..1
    public ICollection<DailyExpense> DailyExpenses { get; set; } = new List<DailyExpense>();
    public ICollection<DailyExpenseRecurrence> DailyExpenseRecurrences { get; set; } = new List<DailyExpenseRecurrence>();
    public ICollection<Expense> ExpensesPaidFromHere { get; set; } = new List<Expense>();
}
