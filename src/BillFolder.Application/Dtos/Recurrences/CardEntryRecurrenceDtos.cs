namespace BillFolder.Application.Dtos.Recurrences;

public sealed record CardEntryRecurrenceResponse(
    Guid Id,
    Guid CardId,
    string CardName,
    string DefaultLabel,
    decimal DefaultAmount,
    Guid DefaultCategoryId,
    string DefaultCategoryName,
    short DayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCardEntryRecurrenceRequest(
    Guid CardId,
    string DefaultLabel,
    decimal DefaultAmount,
    Guid DefaultCategoryId,
    short DayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record UpdateCardEntryRecurrenceRequest(
    Guid? CardId,
    string? DefaultLabel,
    decimal? DefaultAmount,
    Guid? DefaultCategoryId,
    short? DayOfMonth,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? IsActive);
