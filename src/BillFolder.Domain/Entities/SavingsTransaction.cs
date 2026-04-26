using BillFolder.Domain.Enums;

namespace BillFolder.Domain.Entities;

public class SavingsTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SavingsAccountId { get; set; }
    public SavingsTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Label { get; set; }
    public Guid? LinkedTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public SavingsAccount SavingsAccount { get; set; } = null!;
    public SavingsTransaction? LinkedTransaction { get; set; }  // self-ref pra transferências
}
