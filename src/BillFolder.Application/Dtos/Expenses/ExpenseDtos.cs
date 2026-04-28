using BillFolder.Domain.Enums;

namespace BillFolder.Application.Dtos.Expenses;

public sealed record ExpenseResponse(
    Guid Id,
    DateOnly DueDate,
    string Label,
    decimal ExpectedAmount,
    decimal? ActualAmount,
    ExpenseStatus Status,
    DateOnly? PaidDate,
    Guid? PaidFromAccountId,
    string? PaidFromAccountName,
    Guid CategoryId,
    string CategoryName,
    Guid? LinkedCardStatementId,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateExpenseRequest(
    DateOnly DueDate,
    string Label,
    decimal ExpectedAmount,
    Guid CategoryId,
    string? Notes);

/// <summary>
/// PATCH parcial. Se o cliente setar Status=paid sem paid_date/actual_amount,
/// o service preenche automaticamente (today / expected_amount).
/// </summary>
public sealed record UpdateExpenseRequest(
    DateOnly? DueDate,
    string? Label,
    decimal? ExpectedAmount,
    decimal? ActualAmount,
    ExpenseStatus? Status,
    DateOnly? PaidDate,
    Guid? PaidFromAccountId,
    Guid? CategoryId,
    string? Notes);
