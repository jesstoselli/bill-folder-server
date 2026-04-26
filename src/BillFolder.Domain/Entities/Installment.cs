namespace BillFolder.Domain.Entities;

public class Installment
{
    public Guid Id { get; set; }
    public Guid CardEntryId { get; set; }
    public Guid StatementId { get; set; }
    public short InstallmentNumber { get; set; }
    public decimal Amount { get; set; }

    // Navigations
    public CardEntry CardEntry { get; set; } = null!;
    public CardStatement Statement { get; set; } = null!;
}
