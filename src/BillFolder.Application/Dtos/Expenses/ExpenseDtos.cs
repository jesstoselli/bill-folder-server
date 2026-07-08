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

/// <summary>Body do POST /v1/expenses/{id}/update-amount — reajusta o valor
/// POR SESSÃO (OccurrenceAmount) de uma despesa provisionada. O total do mês
/// (ExpectedAmount) é recalculado = novo valor × nº de ocorrências. O escopo
/// decide o alcance: só esta ocorrência, ou esta + as futuras não-pagas (que
/// também atualiza o DefaultAmount do template pra ciclos futuros).</summary>
public sealed record RepriceProvisionedExpenseRequest(
    decimal Amount,
    RecurrenceScope Scope);

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
