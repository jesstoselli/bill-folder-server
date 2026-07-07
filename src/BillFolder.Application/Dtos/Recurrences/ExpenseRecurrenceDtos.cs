using BillFolder.Domain.Enums;

namespace BillFolder.Application.Dtos.Recurrences;

public sealed record ExpenseRecurrenceResponse(
    Guid Id,
    string DefaultLabel,
    decimal DefaultAmount,
    Guid DefaultCategoryId,
    string DefaultCategoryName,
    ExpenseRecurrenceFrequency Frequency,
    short? DueDay,
    short? Weekday,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateExpenseRecurrenceRequest(
    string DefaultLabel,
    decimal DefaultAmount,
    Guid DefaultCategoryId,
    ExpenseRecurrenceFrequency Frequency,
    short? DueDay,
    short? Weekday,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record UpdateExpenseRecurrenceRequest(
    string? DefaultLabel,
    decimal? DefaultAmount,
    Guid? DefaultCategoryId,
    short? DueDay,
    short? Weekday,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? IsActive);
