namespace BillFolder.Application.Dtos.DailyExpenses;

public sealed record DailyExpenseResponse(
    Guid Id,
    DateOnly Date,
    string Label,
    decimal Amount,
    Guid CategoryId,
    string CategoryName,
    Guid AccountId,
    string AccountName,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateDailyExpenseRequest(
    DateOnly Date,
    string Label,
    decimal Amount,
    Guid CategoryId,
    Guid AccountId,
    string? Notes);

/// <summary>
/// PATCH parcial — null = não muda. Para limpar Notes, mande string vazia.
/// </summary>
public sealed record UpdateDailyExpenseRequest(
    DateOnly? Date,
    string? Label,
    decimal? Amount,
    Guid? CategoryId,
    Guid? AccountId,
    string? Notes);
