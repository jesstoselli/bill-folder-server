using BillFolder.Domain.Enums;

namespace BillFolder.Application.Dtos.Savings;

public sealed record SavingsTransactionResponse(
    Guid Id,
    Guid SavingsAccountId,
    SavingsTransactionType Type,
    decimal Amount,
    DateOnly Date,
    string? Label,
    Guid? LinkedTransactionId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateSavingsTransactionRequest(
    Guid SavingsAccountId,
    SavingsTransactionType Type,
    decimal Amount,
    DateOnly Date,
    string? Label,
    Guid? LinkedTransactionId);

public sealed record UpdateSavingsTransactionRequest(
    SavingsTransactionType? Type,
    decimal? Amount,
    DateOnly? Date,
    string? Label,
    Guid? LinkedTransactionId);
