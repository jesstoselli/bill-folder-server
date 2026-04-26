namespace BillFolder.Domain.Entities;

public class CardEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CardId { get; set; }
    public Guid? TemplateId { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public string Label { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public short InstallmentsCount { get; set; }
    public Guid CategoryId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public CreditCardAccount Card { get; set; } = null!;
    public CardEntryRecurrence? Template { get; set; }
    public Category Category { get; set; } = null!;
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}
