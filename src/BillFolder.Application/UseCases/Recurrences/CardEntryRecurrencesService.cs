using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Recurrences;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Recurrences;

public class CardEntryRecurrencesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateCardEntryRecurrenceRequest> _createValidator;
    private readonly IValidator<UpdateCardEntryRecurrenceRequest> _updateValidator;

    public CardEntryRecurrencesService(
        IApplicationDbContext db,
        IValidator<CreateCardEntryRecurrenceRequest> createValidator,
        IValidator<UpdateCardEntryRecurrenceRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<CardEntryRecurrenceResponse>> ListAsync(
        Guid userId, Guid? cardId, bool? activeOnly, CancellationToken ct = default)
    {
        var query = _db.CardEntryRecurrences
            .AsNoTracking()
            .Where(r => r.UserId == userId);

        if (cardId.HasValue)
        {
            query = query.Where(r => r.CardId == cardId.Value);
        }
        if (activeOnly == true)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderBy(r => r.DefaultLabel)
            .Include(r => r.Card)
            .Include(r => r.DefaultCategory)
            .Select(r => new CardEntryRecurrenceResponse(
                r.Id, r.CardId, r.Card.Name,
                r.DefaultLabel, r.DefaultAmount,
                r.DefaultCategoryId, r.DefaultCategory.NamePt,
                r.DayOfMonth, r.StartDate, r.EndDate, r.IsActive,
                r.CreatedAt, r.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<CardEntryRecurrenceResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var rec = await _db.CardEntryRecurrences
            .AsNoTracking()
            .Include(r => r.Card)
            .Include(r => r.DefaultCategory)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        return rec is null
            ? OperationResult.Fail<CardEntryRecurrenceResponse>("not_found", "Recorrência não encontrada.")
            : OperationResult.Ok(MapToResponse(rec));
    }

    public async Task<OperationResult<CardEntryRecurrenceResponse>> CreateAsync(
        Guid userId, CreateCardEntryRecurrenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CardEntryRecurrenceResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var cardOwnedByUser = await _db.CreditCardAccounts
            .AnyAsync(c => c.Id == request.CardId && c.UserId == userId, ct);
        if (!cardOwnedByUser)
        {
            return OperationResult.Fail<CardEntryRecurrenceResponse>(
                "invalid_card",
                "Cartão não existe ou não pertence ao usuário.");
        }

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == request.DefaultCategoryId, ct);
        if (!categoryExists)
        {
            return OperationResult.Fail<CardEntryRecurrenceResponse>(
                "invalid_category", "Categoria não existe.");
        }

        var rec = new CardEntryRecurrence
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CardId = request.CardId,
            DefaultLabel = request.DefaultLabel.Trim(),
            DefaultAmount = request.DefaultAmount,
            DefaultCategoryId = request.DefaultCategoryId,
            DayOfMonth = request.DayOfMonth,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
        };

        _db.CardEntryRecurrences.Add(rec);
        await _db.SaveChangesAsync(ct);

        var created = await _db.CardEntryRecurrences
            .AsNoTracking()
            .Include(r => r.Card)
            .Include(r => r.DefaultCategory)
            .FirstAsync(r => r.Id == rec.Id, ct);

        return OperationResult.Ok(MapToResponse(created));
    }

    public async Task<OperationResult<CardEntryRecurrenceResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateCardEntryRecurrenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CardEntryRecurrenceResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var rec = await _db.CardEntryRecurrences
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (rec is null)
        {
            return OperationResult.Fail<CardEntryRecurrenceResponse>(
                "not_found", "Recorrência não encontrada.");
        }

        var newStart = request.StartDate ?? rec.StartDate;
        var newEnd = request.EndDate ?? rec.EndDate;
        if (newEnd.HasValue && newEnd.Value < newStart)
        {
            return OperationResult.Fail<CardEntryRecurrenceResponse>(
                "validation_error",
                "Data de fim deve ser igual ou posterior à de início.");
        }

        if (request.CardId.HasValue && request.CardId.Value != rec.CardId)
        {
            var cardOwnedByUser = await _db.CreditCardAccounts
                .AnyAsync(c => c.Id == request.CardId.Value && c.UserId == userId, ct);
            if (!cardOwnedByUser)
            {
                return OperationResult.Fail<CardEntryRecurrenceResponse>(
                    "invalid_card",
                    "Cartão não existe ou não pertence ao usuário.");
            }
            rec.CardId = request.CardId.Value;
        }

        if (request.DefaultCategoryId.HasValue && request.DefaultCategoryId.Value != rec.DefaultCategoryId)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.DefaultCategoryId.Value, ct);
            if (!categoryExists)
            {
                return OperationResult.Fail<CardEntryRecurrenceResponse>(
                    "invalid_category", "Categoria não existe.");
            }
            rec.DefaultCategoryId = request.DefaultCategoryId.Value;
        }

        if (request.DefaultLabel is not null)
        {
            rec.DefaultLabel = request.DefaultLabel.Trim();
        }
        if (request.DefaultAmount.HasValue)
        {
            rec.DefaultAmount = request.DefaultAmount.Value;
        }
        if (request.DayOfMonth.HasValue)
        {
            rec.DayOfMonth = request.DayOfMonth.Value;
        }
        if (request.StartDate.HasValue)
        {
            rec.StartDate = request.StartDate.Value;
        }
        if (request.EndDate.HasValue)
        {
            rec.EndDate = request.EndDate.Value;
        }
        if (request.IsActive.HasValue)
        {
            rec.IsActive = request.IsActive.Value;
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.CardEntryRecurrences
            .AsNoTracking()
            .Include(r => r.Card)
            .Include(r => r.DefaultCategory)
            .FirstAsync(r => r.Id == id, ct);

        return OperationResult.Ok(MapToResponse(updated));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var rec = await _db.CardEntryRecurrences
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (rec is null)
        {
            return OperationResult.Fail<bool>("not_found", "Recorrência não encontrada.");
        }

        _db.CardEntryRecurrences.Remove(rec);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    private static CardEntryRecurrenceResponse MapToResponse(CardEntryRecurrence r) =>
        new(r.Id, r.CardId, r.Card.Name,
            r.DefaultLabel, r.DefaultAmount,
            r.DefaultCategoryId, r.DefaultCategory.NamePt,
            r.DayOfMonth, r.StartDate, r.EndDate, r.IsActive,
            r.CreatedAt, r.UpdatedAt);
}
