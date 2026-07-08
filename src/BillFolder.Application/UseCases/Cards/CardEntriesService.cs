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
    private readonly IValidator<UpdateCardSubscriptionAmountRequest> _updateAmountValidator;

    public CardEntriesService(
        IApplicationDbContext db,
        IValidator<CreateCardEntryRequest> createValidator,
        IValidator<UpdateCardEntryRequest> updateValidator,
        IValidator<UpdateCardSubscriptionAmountRequest> updateAmountValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _updateAmountValidator = updateAmountValidator;
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

        var entry = await MaterializeChargeAsync(
            _db,
            userId,
            card,
            request.PurchaseDate,
            request.Label.Trim(),
            request.TotalAmount,
            request.InstallmentsCount,
            request.CategoryId,
            templateId: null,
            NormalizeOptional(request.Notes),
            ct);

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
        Guid userId, Guid id, RecurrenceScope scope = RecurrenceScope.This, CancellationToken ct = default)
    {
        var entry = await _db.CardEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (entry is null)
        {
            return OperationResult.Fail<bool>("not_found", "Compra não encontrada.");
        }

        // ThisAndFollowing só faz sentido numa assinatura (com TemplateId): apaga esta +
        // as ocorrências seguintes cuja fatura ainda está aberta E encerra o template
        // (IsActive = false) pra parar a geração futura. Sem TemplateId, cai no This.
        if (scope == RecurrenceScope.ThisAndFollowing && entry.TemplateId is { } templateId)
        {
            var siblings = await _db.CardEntries
                .Where(e => e.UserId == userId && e.TemplateId == templateId)
                .Include(e => e.Installments).ThenInclude(i => i.Statement)
                .ToListAsync(ct);

            var statusByEntryId = SubscriptionStatementStatuses(siblings);

            var idsToDelete = SubscriptionOccurrencesToDelete(siblings, entry, scope, statusByEntryId);
            var toDelete = siblings.Where(e => idsToDelete.Contains(e.Id));

            // FK em installments com ON DELETE CASCADE — parcelas vão junto.
            _db.CardEntries.RemoveRange(toDelete);

            var template = await _db.CardEntryRecurrences
                .FirstOrDefaultAsync(r => r.Id == templateId && r.UserId == userId, ct);
            if (template is not null)
            {
                template.IsActive = false;
            }
        }
        else
        {
            // FK em installments com ON DELETE CASCADE — parcelas vão junto.
            // Faturas (statements) ficam: podem ter outras compras vinculadas.
            _db.CardEntries.Remove(entry);
        }

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    /// <summary>
    /// Reajusta o valor de uma assinatura (CardEntry mensal, InstallmentsCount = 1).
    /// This → só esta ocorrência. ThisAndFollowing → esta + as seguintes em fatura aberta,
    /// e ainda atualiza o DefaultAmount do template pra gerações futuras usarem o novo preço.
    /// Cada ocorrência tem uma única installment — recalcula tanto a installment quanto o
    /// TotalAmount do entry. Faturas fechadas/pagas nunca mudam.
    /// </summary>
    public async Task<OperationResult<CardEntryResponse>> UpdateSubscriptionAmountAsync(
        Guid userId, Guid cardEntryId, UpdateCardSubscriptionAmountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateAmountValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CardEntryResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var entry = await _db.CardEntries
            .Where(e => e.Id == cardEntryId && e.UserId == userId)
            .Include(e => e.Installments).ThenInclude(i => i.Statement)
            .FirstOrDefaultAsync(ct);

        if (entry is null)
        {
            return OperationResult.Fail<CardEntryResponse>("not_found", "Compra não encontrada.");
        }

        // Reajuste só existe pra assinatura: mensal, uma parcela. Compra parcelada muda
        // por outro fluxo (recalcular N parcelas e mover entre faturas).
        if (entry.InstallmentsCount != 1)
        {
            return OperationResult.Fail<CardEntryResponse>(
                "not_subscription", "Reajuste só se aplica a assinatura (uma parcela).");
        }

        if (request.Scope == RecurrenceScope.ThisAndFollowing && entry.TemplateId is { } templateId)
        {
            var siblings = await _db.CardEntries
                .Where(e => e.UserId == userId && e.TemplateId == templateId)
                .Include(e => e.Installments).ThenInclude(i => i.Statement)
                .ToListAsync(ct);

            var statusByEntryId = SubscriptionStatementStatuses(siblings);
            var idsToReprice = SubscriptionOccurrencesToReprice(siblings, entry, request.Scope, statusByEntryId);

            foreach (var sibling in siblings.Where(e => idsToReprice.Contains(e.Id)))
            {
                RepriceSubscriptionEntry(sibling, request.Amount);
            }

            var template = await _db.CardEntryRecurrences
                .FirstOrDefaultAsync(r => r.Id == templateId && r.UserId == userId, ct);
            if (template is not null)
            {
                template.DefaultAmount = request.Amount;
            }
        }
        else
        {
            // This (ou sem template): só a ocorrência-alvo.
            RepriceSubscriptionEntry(entry, request.Amount);
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.CardEntries
            .AsNoTracking()
            .Where(e => e.Id == cardEntryId)
            .Include(e => e.Card)
            .Include(e => e.Category)
            .Include(e => e.Installments).ThenInclude(i => i.Statement)
            .FirstAsync(ct);

        return OperationResult.Ok(MapToResponse(updated));
    }

    // ----- helpers -----

    /// <summary>
    /// Aplica o novo valor a uma assinatura: a única installment recebe o valor cheio
    /// e o TotalAmount do entry acompanha.
    /// </summary>
    private static void RepriceSubscriptionEntry(CardEntry entry, decimal amount)
    {
        entry.TotalAmount = amount;
        var installment = entry.Installments.FirstOrDefault();
        if (installment is not null)
        {
            installment.Amount = amount;
        }
    }

    /// <summary>
    /// Seleciona as ocorrências de uma assinatura (CardEntry mensal com TemplateId) a
    /// excluir, dado o escopo:
    /// <list type="bullet">
    /// <item><see cref="RecurrenceScope.This"/> → apenas a ocorrência-alvo.</item>
    /// <item><see cref="RecurrenceScope.ThisAndFollowing"/> → a alvo + as com
    /// <c>PurchaseDate &gt;= target.PurchaseDate</c> cuja fatura ainda está aberta
    /// (<see cref="CardStatementStatus.Open"/>). Ocorrências futuras já em fatura
    /// fechada/paga (Closed/Paid) permanecem, preservando o histórico.</item>
    /// </list>
    /// Puro (sem DB): o status da fatura de cada entry entra via dicionário, mantendo
    /// o helper testável. Uma assinatura tem exatamente uma installment numa fatura.
    /// </summary>
    internal static IReadOnlyCollection<Guid> SubscriptionOccurrencesToDelete(
        IReadOnlyCollection<CardEntry> templateEntries,
        CardEntry target,
        RecurrenceScope scope,
        IReadOnlyDictionary<Guid, CardStatementStatus> statementStatusByEntryId)
        => SubscriptionOccurrencesInScope(templateEntries, target, scope, statementStatusByEntryId);

    /// <summary>
    /// Seleciona as ocorrências de uma assinatura que recebem o novo valor num reajuste,
    /// dado o escopo. Mesma regra do <see cref="SubscriptionOccurrencesToDelete"/>:
    /// <see cref="RecurrenceScope.This"/> → só a alvo; <see cref="RecurrenceScope.ThisAndFollowing"/>
    /// → a alvo + as seguintes em fatura aberta (fechadas/pagas nunca mudam).
    /// </summary>
    internal static IReadOnlyCollection<Guid> SubscriptionOccurrencesToReprice(
        IReadOnlyCollection<CardEntry> templateEntries,
        CardEntry target,
        RecurrenceScope scope,
        IReadOnlyDictionary<Guid, CardStatementStatus> statementStatusByEntryId)
        => SubscriptionOccurrencesInScope(templateEntries, target, scope, statementStatusByEntryId);

    /// <summary>
    /// Núcleo compartilhado por delete e reprice: dado o escopo, retorna os ids das
    /// ocorrências afetadas. This → só a alvo. ThisAndFollowing → a alvo + as com
    /// PurchaseDate &gt;= alvo cuja fatura ainda está aberta (Closed/Paid ficam de fora).
    /// </summary>
    private static IReadOnlyCollection<Guid> SubscriptionOccurrencesInScope(
        IReadOnlyCollection<CardEntry> templateEntries,
        CardEntry target,
        RecurrenceScope scope,
        IReadOnlyDictionary<Guid, CardStatementStatus> statementStatusByEntryId)
    {
        ArgumentNullException.ThrowIfNull(templateEntries);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(statementStatusByEntryId);

        if (scope == RecurrenceScope.This)
        {
            return new[] { target.Id };
        }

        var ids = new HashSet<Guid> { target.Id };
        foreach (var entry in templateEntries)
        {
            if (entry.PurchaseDate < target.PurchaseDate)
            {
                continue;
            }

            // Só cai fora quem tem fatura fechada/paga. Sem status no dicionário → trata
            // como aberta (não deveria acontecer, mas não perde a ocorrência-alvo).
            var isClosedOrPaid =
                statementStatusByEntryId.TryGetValue(entry.Id, out var status)
                && status != CardStatementStatus.Open;

            if (!isClosedOrPaid)
            {
                ids.Add(entry.Id);
            }
        }
        return ids;
    }

    /// <summary>
    /// Mapeia cada assinatura (CardEntry com uma única installment) ao status da sua
    /// fatura. Assume entries com Installments+Statement já carregados. Entries sem
    /// installment (não deveria acontecer numa assinatura) ficam de fora do dicionário.
    /// </summary>
    private static Dictionary<Guid, CardStatementStatus> SubscriptionStatementStatuses(
        IEnumerable<CardEntry> entries)
    {
        var map = new Dictionary<Guid, CardStatementStatus>();
        foreach (var entry in entries)
        {
            var statement = entry.Installments.FirstOrDefault()?.Statement;
            if (statement is not null)
            {
                map[entry.Id] = statement.Status;
            }
        }
        return map;
    }

    /// <summary>
    /// Materializa UMA compra: cria o CardEntry, distribui parcelas e faz
    /// find-or-create das CardStatements que as recebem (via CardCycleCalculator).
    /// Só ADICIONA ao change tracker — o SaveChangesAsync é responsabilidade do
    /// caller (CreateAsync salva logo após; a expansão de recorrências salva em
    /// lote). Reutilizado por CardEntryRecurrenceExpansion pra auto-gerar
    /// assinaturas mensais como uma compra à vista (installmentsCount = 1).
    /// </summary>
    internal static async Task<CardEntry> MaterializeChargeAsync(
        IApplicationDbContext db,
        Guid userId,
        CreditCardAccount card,
        DateOnly purchaseDate,
        string label,
        decimal totalAmount,
        short installmentsCount,
        Guid categoryId,
        Guid? templateId,
        string? notes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(card);

        // Calcula a fatura da PRIMEIRA parcela
        var (firstStart, firstEnd, firstDue) = CardCycleCalculator.ComputeStatementForPurchase(
            purchaseDate, card.ClosingDay, card.DueDay);

        // Calcula o último period_end (parcela N)
        var lastEnd = firstEnd;
        var lastStart = firstStart;
        var lastDue = firstDue;
        for (var i = 1; i < installmentsCount; i++)
        {
            (lastStart, lastEnd, lastDue) = CardCycleCalculator.NextStatement(
                lastEnd, card.ClosingDay, card.DueDay);
        }

        // Pre-load todas as faturas que JÁ existem nesse range pra evitar N queries
        var existingStatements = await db.CardStatements
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
            TemplateId = templateId,
            PurchaseDate = purchaseDate,
            Label = label,
            TotalAmount = totalAmount,
            InstallmentsCount = installmentsCount,
            CategoryId = categoryId,
            Notes = notes,
        };
        db.CardEntries.Add(entry);

        // Distribui valores entre parcelas (última pega o resto pra fechar conta)
        var amounts = CardCycleCalculator.DistributeAmounts(totalAmount, installmentsCount);

        // Para cada parcela, find-or-create a statement e cria a installment
        var currentStart = firstStart;
        var currentEnd = firstEnd;
        var currentDue = firstDue;

        for (var i = 0; i < installmentsCount; i++)
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
                db.CardStatements.Add(statement);
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
            db.Installments.Add(installment);

            // Avança pra próxima fatura (se não for a última)
            if (i < installmentsCount - 1)
            {
                (currentStart, currentEnd, currentDue) = CardCycleCalculator.NextStatement(
                    currentEnd, card.ClosingDay, card.DueDay);
            }
        }

        return entry;
    }

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
