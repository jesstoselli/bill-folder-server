using BillFolder.Domain.Enums;

namespace BillFolder.Application.Dtos.Cards;

public sealed record CardStatementResponse(
    Guid Id,
    Guid CardId,
    string CardName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    CardStatementStatus Status,
    decimal TotalAmount,
    int InstallmentsCount,
    Guid? LinkedExpenseId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CardStatementDetailResponse(
    Guid Id,
    Guid CardId,
    string CardName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    CardStatementStatus Status,
    decimal TotalAmount,
    Guid? LinkedExpenseId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<StatementInstallmentDto> Installments);

public sealed record StatementInstallmentDto(
    Guid InstallmentId,
    Guid CardEntryId,
    short InstallmentNumber,
    decimal Amount,
    DateOnly PurchaseDate,
    string Label,
    string CategoryName);

public sealed record UpdateCardStatementRequest(
    CardStatementStatus? Status);
