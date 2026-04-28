namespace BillFolder.Application.Dtos.Recurrences;

public sealed record DailyExpenseRecurrenceResponse(
    Guid Id,
    string DefaultLabel,
    decimal DefaultAmount,
    Guid DefaultCategoryId,
    string DefaultCategoryName,
    Guid DefaultAccountId,
    string DefaultAccountName,
    short DayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateDailyExpenseRecurrenceRequest(
    string DefaultLabel,
    decimal DefaultAmount,
    Guid DefaultCategoryId,
    Guid DefaultAccountId,
    short DayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record UpdateDailyExpenseRecurrenceRequest(
    string? DefaultLabel,
    decimal? DefaultAmount,
    Guid? DefaultCategoryId,
    Guid? DefaultAccountId,
    short? DayOfMonth,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? IsActive);
