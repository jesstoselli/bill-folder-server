namespace BillFolder.Domain.Entities;

public class Cycle
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Label { get; set; } = null!;
    public bool IsRecurrenceGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
}
