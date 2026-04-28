namespace BillFolder.Application.Dtos.Cards;

public sealed record CardEntryResponse(
    Guid Id,
    Guid CardId,
    string CardName,
    DateOnly PurchaseDate,
    string Label,
    decimal TotalAmount,
    short InstallmentsCount,
    Guid CategoryId,
    string CategoryName,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<EntryInstallmentDto> Installments);

public sealed record EntryInstallmentDto(
    Guid InstallmentId,
    short InstallmentNumber,
    decimal Amount,
    Guid StatementId,
    DateOnly StatementDueDate);

public sealed record CreateCardEntryRequest(
    Guid CardId,
    DateOnly PurchaseDate,
    string Label,
    decimal TotalAmount,
    short InstallmentsCount,
    Guid CategoryId,
    string? Notes);

/// <summary>
/// Atualização limitada: label, categoria, notes. Mudar valor/parcelas/data
/// exigiria recalcular todas as installments e mover entre faturas — operação
/// complexa que merece endpoint dedicado no futuro.
/// </summary>
public sealed record UpdateCardEntryRequest(
    string? Label,
    Guid? CategoryId,
    string? Notes);
