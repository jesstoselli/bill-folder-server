namespace BillFolder.Domain.Entities;

public class SavingsAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CheckingAccountId { get; set; }
    public string BankName { get; set; } = null!;
    public string? Branch { get; set; }
    public string? AccountNumber { get; set; }
    public decimal InitialBalance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public CheckingAccount CheckingAccount { get; set; } = null!;
    public ICollection<SavingsTransaction> Transactions { get; set; } = new List<SavingsTransaction>();
}
