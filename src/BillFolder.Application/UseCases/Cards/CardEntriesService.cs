using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Cards;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Cards;

public class CardEntriesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateCardEntryRequest> _createValidator;
    private readonly IValidator<UpdateCardEntryRequest> _updateValidator;

    public CardEntriesService(
        IApplicationDbContext db,
        IValidator<CreateCardEntryRequest> createValidator,
        IValidator<UpdateCardEntryRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<CardEntryResponse>> ListAsync(
        Guid userId,
        Guid? cardId,
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        CancellationToken ct = default)
    {
        var query = _db.CardEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId);

        if (cardId.HasValue)
        {
            query = query.Where(e => e.CardId == cardId.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(e => e.PurchaseDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(e => e.PurchaseDate <= to.Value);
        }
        if (categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId.Value);
        }

        var entries = await query
            .OrderByDescending(e => e.PurchaseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Include(e => e.Card)
            .Include(e => e.Category)
            .Include(e => e.Installments).ThenInclude(i => i.Statement)
            .ToListAsync(ct);

        return entries.Select(MapToResponse).ToList();
    }

    public async Task<OperationResult<CardEntryResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var entry = await _db.CardEntries
            .AsNoTracking()
            .Where(e => e.Id == id && e.UserId == userId)
            .Include(e => e.Card)
            .Include(e => e.Category)
            .Include(e => e.Installments).ThenInclude(i => i.Statement)
            .FirstOrDefaultAsync(ct);

        return entry is null
            ? OperationResult.Fail<CardEntryResponse>("not_found", "Compra não encontrada.")
            : OperationResult.Ok(MapToResponse(entry));
    }

    public async Task<OperationResult<CardEntryResponse>> CreateAsync(
        Guid userId, CreateCardEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CardEntryResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        // Carrega o cartão (precisa de closing_day e due_day pra calcular faturas)
        var card = await _db.CreditCardAccounts
            .FirstOrDefaultAsync(c => c.Id == request.CardId && c.UserId == userId, ct);
        if (card is null)
        {
            return OperationResult.Fail<CardEntryResponse>(
                "invalid_card",
                "Cartão não existe ou não pertence ao usuário.");
        }

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
        {
            return OperationResult.Fail<CardEntryResponse>(
                "invalid_category", "Categoria não existe.");
        }

        // Calcula a fatura da PRIMEIRA parcela
        var (firstStart, firstEnd, firstDue) = CardCycleCalculator.ComputeStatementForPurchase(
            request.PurchaseDate, card.ClosingDay, card.DueDay);

        // Calcula o último period_end (parcela N)
        var lastEnd = firstEnd;
        var lastStart = firstStart;
        var lastDue = firstDue;
        for (var i = 1; i < request.InstallmentsCount; i++)
        {
            (lastStart, lastEnd, lastDue) = CardCycleCalculator.NextStatement(
                lastEnd, card.ClosingDay, card.DueDay);
        }

        // Pre-load todas as faturas que JÁ existem nesse range pra evitar N queries
        var existingStatements = await _db.CardStatements
            .Where(s => s.CardId == card.Id
                     && s.PeriodEnd >= firstEnd
                     && s.PeriodEnd <= lastEnd)
            .ToListAsync(ct);

        var statementCache = existingStatements.ToDictionary(s => s.PeriodEnd);

        // Cria a entry
        var entry = new CardEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CardId = card.Id,
            PurchaseDate = request.PurchaseDate,
            Label = request.Label.Trim(),
            TotalAmount = request.TotalAmount,
            InstallmentsCount = request.InstallmentsCount,
            CategoryId = request.CategoryId,
            Notes = NormalizeOptional(request.Notes),
        };
        _db.CardEntries.Add(entry);

        // Distribui valores entre parcelas (última pega o resto pra fechar conta)
        var amounts = CardCycleCalculator.DistributeAmounts(request.TotalAmount, request.InstallmentsCount);

        // Para cada parcela, find-or-create a statement e cria a installment
        var currentStart = firstStart;
        var currentEnd = firstEnd;
        var currentDue = firstDue;

        for (var i = 0; i < request.InstallmentsCount; i++)
        {
            if (!statementCache.TryGetValue(currentEnd, out var statement))
            {
                statement = new CardStatement
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    CardId = card.Id,
                    PeriodStart = currentStart,
                    PeriodEnd = currentEnd,
                    DueDate = currentDue,
                    Status = CardStatementStatus.Open,
                };
                _db.CardStatements.Add(statement);
                statementCache[currentEnd] = statement;
            }

            var installment = new Installment
            {
                Id = Guid.CreateVersion7(),
                CardEntryId = entry.Id,
                StatementId = statement.Id,
                InstallmentNumber = (short)(i + 1),
                Amount = amounts[i],
            };
            _db.Installments.Add(installment);

            // Avança pra próxima fatura (se não for a última)
            if (i < request.InstallmentsCount - 1)
            {
                (currentStart, currentEnd, currentDue) = CardCycleCalculator.NextStatement(
                    currentEnd, card.ClosingDay, card.DueDay);
            }
        }

        // SaveChanges único — toda a operação numa transação implícita do EF Core
        await _db.SaveChangesAsync(ct);

        // Reload pra retornar com nav properties hidratadas
        var created = await _db.CardEntries
            .AsNoTracking()
            .Where(e => e.Id == entry.Id)
            .Include(e => e.Card)
            .Include(e => e.Category)
            .Include(e => e.Installments).ThenInclude(i => i.Statement)
            .FirstAsync(ct);

        return OperationResult.Ok(MapToResponse(created));
    }

    public async Task<OperationResult<CardEntryResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateCardEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CardEntryResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var entry = await _db.CardEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (entry is null)
        {
            return OperationResult.Fail<CardEntryResponse>(
                "not_found", "Compra não encontrada.");
        }

        if (request.CategoryId.HasValue && request.CategoryId.Value != entry.CategoryId)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, ct);
            if (!categoryExists)
            {
                return OperationResult.Fail<CardEntryResponse>(
                    "invalid_category", "Categoria não existe.");
            }
            entry.CategoryId = request.CategoryId.Value;
        }

        if (request.Label is not null)
        {
            entry.Label = request.Label.Trim();
        }
        if (request.Notes is not null)
        {
            entry.Notes = NormalizeOptional(request.Notes);
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.CardEntries
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Include(e => e.Card)
            .Include(e => e.Category)
            .Include(e => e.Installments).ThenInclude(i => i.Statement)
            .FirstAsync(ct);

        return OperationResult.Ok(MapToResponse(updated));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var entry = await _db.CardEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (entry is null)
        {
            return OperationResult.Fail<bool>("not_found", "Compra não encontrada.");
        }

        // FK em installments com ON DELETE CASCADE — parcelas vão junto.
        // Faturas (statements) ficam: podem ter outras compras vinculadas.
        _db.CardEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    // ----- helpers -----

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }
        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CardEntryResponse MapToResponse(CardEntry e) =>
        new(
            e.Id,
            e.CardId,
            e.Card.Name,
            e.PurchaseDate,
            e.Label,
            e.TotalAmount,
            e.InstallmentsCount,
            e.CategoryId,
            e.Category.NamePt,
            e.Notes,
            e.CreatedAt,
            e.UpdatedAt,
            e.Installments
                .OrderBy(i => i.InstallmentNumber)
                .Select(i => new EntryInstallmentDto(
                    i.Id,
                    i.InstallmentNumber,
                    i.Amount,
                    i.StatementId,
                    i.Statement.DueDate))
                .ToList());
}
