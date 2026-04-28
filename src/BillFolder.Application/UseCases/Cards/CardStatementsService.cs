using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Cards;
using BillFolder.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Cards;

public class CardStatementsService
{
    private readonly IApplicationDbContext _db;

    public CardStatementsService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<CardStatementResponse>> ListAsync(
        Guid userId,
        Guid? cardId,
        CardStatementStatus? status,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var query = _db.CardStatements
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        if (cardId.HasValue)
        {
            query = query.Where(s => s.CardId == cardId.Value);
        }
        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(s => s.DueDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(s => s.DueDate <= to.Value);
        }

        return await query
            .OrderByDescending(s => s.DueDate)
            .Select(s => new CardStatementResponse(
                s.Id,
                s.CardId,
                s.Card.Name,
                s.PeriodStart,
                s.PeriodEnd,
                s.DueDate,
                s.Status,
                s.Installments.Sum(i => i.Amount),
                s.Installments.Count,
                s.LinkedExpenseId,
                s.CreatedAt,
                s.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<CardStatementDetailResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var statement = await _db.CardStatements
            .AsNoTracking()
            .Where(s => s.Id == id && s.UserId == userId)
            .Include(s => s.Card)
            .Include(s => s.Installments).ThenInclude(i => i.CardEntry).ThenInclude(e => e.Category)
            .FirstOrDefaultAsync(ct);

        if (statement is null)
        {
            return OperationResult.Fail<CardStatementDetailResponse>(
                "not_found", "Fatura não encontrada.");
        }

        var installments = statement.Installments
            .OrderBy(i => i.CardEntry.PurchaseDate)
            .ThenBy(i => i.InstallmentNumber)
            .Select(i => new StatementInstallmentDto(
                i.Id,
                i.CardEntryId,
                i.InstallmentNumber,
                i.Amount,
                i.CardEntry.PurchaseDate,
                i.CardEntry.Label,
                i.CardEntry.Category.NamePt))
            .ToList();

        var totalAmount = installments.Sum(i => i.Amount);

        var response = new CardStatementDetailResponse(
            statement.Id,
            statement.CardId,
            statement.Card.Name,
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.DueDate,
            statement.Status,
            totalAmount,
            statement.LinkedExpenseId,
            statement.CreatedAt,
            statement.UpdatedAt,
            installments);

        return OperationResult.Ok(response);
    }

    public async Task<OperationResult<CardStatementResponse>> UpdateStatusAsync(
        Guid userId, Guid id, UpdateCardStatementRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Status.HasValue)
        {
            return OperationResult.Fail<CardStatementResponse>(
                "validation_error", "Status é obrigatório.");
        }

        var statement = await _db.CardStatements
            .Include(s => s.Card)
            .Include(s => s.Installments)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (statement is null)
        {
            return OperationResult.Fail<CardStatementResponse>(
                "not_found", "Fatura não encontrada.");
        }

        statement.Status = request.Status.Value;

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(new CardStatementResponse(
            statement.Id,
            statement.CardId,
            statement.Card.Name,
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.DueDate,
            statement.Status,
            statement.Installments.Sum(i => i.Amount),
            statement.Installments.Count,
            statement.LinkedExpenseId,
            statement.CreatedAt,
            statement.UpdatedAt));
    }
}
