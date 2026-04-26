using BillFolder.Domain.Enums;

namespace BillFolder.Domain.Entities;

public class IncomeEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? SourceId { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public DateOnly ExpectedDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    public IncomeStatus Status { get; set; } = IncomeStatus.Expected;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public IncomeSource? Source { get; set; }
}
