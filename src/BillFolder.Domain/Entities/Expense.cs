using BillFolder.Domain.Enums;

namespace BillFolder.Domain.Entities;

public class Expense
{
  public Guid Id { get; set; }
  public Guid UserId { get; set; }
  public Guid? TemplateId { get; set; }
  public DateOnly DueDate { get; set; }
  public string Label { get; set; } = null!;
  public decimal ExpectedAmount { get; set; }
  public decimal? ActualAmount { get; set; }
  public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;
  public DateOnly? PaidDate { get; set; }
  public Guid? PaidFromAccountId { get; set; }
  public Guid CategoryId { get; set; }
  public Guid? LinkedCardStatementId { get; set; }
  public string? Notes { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime UpdatedAt { get; set; }

  // Navigations
  public User User { get; set; } = null!;
  public ExpenseRecurrence? Template { get; set; }
  public CheckingAccount? PaidFromAccount { get; set; }
  public Category Category { get; set; } = null!;
  public CardStatement? LinkedCardStatement { get; set; }
}
