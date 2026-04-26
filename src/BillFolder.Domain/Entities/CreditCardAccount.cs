namespace BillFolder.Domain.Entities;

public class CreditCardAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? IssuerBank { get; set; }
    public string? Brand { get; set; }
    public short ClosingDay { get; set; }
    public short DueDay { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public ICollection<CardEntry> CardEntries { get; set; } = new List<CardEntry>();
    public ICollection<CardEntryRecurrence> CardEntryRecurrences { get; set; } = new List<CardEntryRecurrence>();
    public ICollection<CardStatement> Statements { get; set; } = new List<CardStatement>();
}
