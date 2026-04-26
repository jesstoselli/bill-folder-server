namespace BillFolder.Domain.Entities;

public class CardEntryRecurrence
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CardId { get; set; }
    public string DefaultLabel { get; set; } = null!;
    public decimal DefaultAmount { get; set; }
    public Guid DefaultCategoryId { get; set; }
    public short DayOfMonth { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public CreditCardAccount Card { get; set; } = null!;
    public Category DefaultCategory { get; set; } = null!;
    public ICollection<CardEntry> Instances { get; set; } = new List<CardEntry>();
}
