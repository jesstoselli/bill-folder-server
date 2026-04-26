namespace BillFolder.Domain.Entities;

public class DailyExpense
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? TemplateId { get; set; }
    public DateOnly Date { get; set; }
    public Guid CategoryId { get; set; }
    public string Label { get; set; } = null!;
    public decimal Amount { get; set; }
    public Guid AccountId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public DailyExpenseRecurrence? Template { get; set; }
    public Category Category { get; set; } = null!;
    public CheckingAccount Account { get; set; } = null!;
}
