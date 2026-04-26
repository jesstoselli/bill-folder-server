using BillFolder.Domain.Enums;

namespace BillFolder.Domain.Entities;

public class CycleAdjustment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public CycleAdjustmentType Type { get; set; }
    public string Label { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public Guid? SourceSavingsTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public SavingsTransaction? SourceSavingsTransaction { get; set; }
}
