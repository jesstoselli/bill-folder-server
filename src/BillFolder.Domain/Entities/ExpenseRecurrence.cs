using BillFolder.Domain.Enums;

namespace BillFolder.Domain.Entities;

public class ExpenseRecurrence
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DefaultLabel { get; set; } = null!;
    public decimal DefaultAmount { get; set; }
    public Guid DefaultCategoryId { get; set; }
    public ExpenseRecurrenceFrequency Frequency { get; set; } = ExpenseRecurrenceFrequency.Monthly;
    /// <summary>Dia do mês (1–31). Usado só quando Frequency == Monthly. Null pra Weekly.</summary>
    public short? DueDay { get; set; }
    /// <summary>Dia da semana (0=domingo … 6=sábado, casa com DayOfWeek). Usado só quando Frequency == Weekly.</summary>
    public short? Weekday { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
    public Category DefaultCategory { get; set; } = null!;
    public ICollection<Expense> Instances { get; set; } = new List<Expense>();
}
