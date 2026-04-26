using BillFolder.Domain.Enums;

namespace BillFolder.Domain.Entities;

public class CardStatement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CardId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly DueDate { get; set; }
    public CardStatementStatus Status { get; set; } = CardStatementStatus.Open;
    public Guid? LinkedExpenseId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public CreditCardAccount Card { get; set; } = null!;
    public Expense? LinkedExpense { get; set; }
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
    public ICollection<Expense> RelatedExpenses { get; set; } = new List<Expense>(); // expenses.linked_card_statement_id
}
