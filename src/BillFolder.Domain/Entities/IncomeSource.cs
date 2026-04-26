using BillFolder.Domain.Enums;

namespace BillFolder.Domain.Entities;

public class IncomeSource
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Origin { get; set; } = null!;
    public IncomeOriginType OriginType { get; set; }
    public decimal DefaultAmount { get; set; }
    public short ExpectedDay { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public ICollection<IncomeEntry> Entries { get; set; } = new List<IncomeEntry>();
}
