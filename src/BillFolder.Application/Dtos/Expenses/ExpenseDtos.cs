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
    // Provisionamento (null/0 em despesas normais):
    decimal? OccurrenceAmount,
    int? OccurrencesTotal,
    int OccurrencesPaid,
    decimal PaidToDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Body do POST /v1/expenses/{id}/pay-occurrence — dá baixa em UMA
/// ocorrência (semana) de uma despesa provisionada.</summary>
public sealed record PayOccurrenceRequest(
    DateOnly? PaidDate,
    decimal? Amount,
    Guid? PaidFromAccountId);

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
