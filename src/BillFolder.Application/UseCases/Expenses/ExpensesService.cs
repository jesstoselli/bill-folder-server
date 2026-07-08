using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Expenses;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Expenses;

public class ExpensesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateExpenseRequest> _createValidator;
    private readonly IValidator<UpdateExpenseRequest> _updateValidator;
    private readonly IValidator<RepriceProvisionedExpenseRequest> _repriceValidator;

    public ExpensesService(
        IApplicationDbContext db,
        IValidator<CreateExpenseRequest> createValidator,
        IValidator<UpdateExpenseRequest> updateValidator,
        IValidator<RepriceProvisionedExpenseRequest> repriceValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _repriceValidator = repriceValidator;
    }

    public async Task<List<ExpenseResponse>> ListAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        ExpenseStatus? status,
        Guid? categoryId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var query = _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId);

        if (from.HasValue)
        {
            query = query.Where(e => e.DueDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(e => e.DueDate <= to.Value);
        }
        if (categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId.Value);
        }

        // Filtro de status considera overdue computado
        if (status.HasValue)
        {
            query = status.Value switch
            {
                ExpenseStatus.Pending  => query.Where(e => e.Status == ExpenseStatus.Pending && e.DueDate >= today),
                ExpenseStatus.Overdue  => query.Where(e => e.Status == ExpenseStatus.Overdue
                                                       || (e.Status == ExpenseStatus.Pending && e.DueDate < today)),
                ExpenseStatus.Paid     => query.Where(e => e.Status == ExpenseStatus.Paid),
                _ => query,
            };
        }

        var expenses = await query
            .OrderBy(e => e.DueDate)
            .ThenByDescending(e => e.CreatedAt)
            .Include(e => e.Category)
            .Include(e => e.PaidFromAccount)
            .ToListAsync(ct);

        return expenses.Select(e => MapToResponse(e, today)).ToList();
    }

    public async Task<OperationResult<ExpenseResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var expense = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.PaidFromAccount)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (expense is null)
        {
            return OperationResult.Fail<ExpenseResponse>("not_found", "Despesa não encontrada.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(expense, today));
    }

    public async Task<OperationResult<ExpenseResponse>> CreateAsync(
        Guid userId, CreateExpenseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "invalid_category", "Categoria não existe.");
        }

        var expense = new Expense
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DueDate = request.DueDate,
            Label = request.Label.Trim(),
            ExpectedAmount = request.ExpectedAmount,
            Status = ExpenseStatus.Pending,
            CategoryId = request.CategoryId,
            Notes = NormalizeOptional(request.Notes),
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);

        var created = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.PaidFromAccount)
            .FirstAsync(e => e.Id == expense.Id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(created, today));
    }

    public async Task<OperationResult<ExpenseResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateExpenseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var expense = await _db.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (expense is null)
        {
            return OperationResult.Fail<ExpenseResponse>("not_found", "Despesa não encontrada.");
        }

        // Despesa provisionada é quitada por ocorrência (pay-occurrence), nunca
        // marcada Paid direto — senão pularia o rastreio semanal e a matemática
        // de PaidToDate. Blinda o caso (o app já roteia pro fluxo certo).
        if (request.Status == ExpenseStatus.Paid && expense.OccurrencesTotal is not null)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "provisioned_expense",
                "Despesa provisionada é quitada por ocorrência, não diretamente.");
        }

        // Editar o total (ExpectedAmount) numa provisionada quebraria o invariante
        // ExpectedAmount = OccurrenceAmount × OccurrencesTotal. O reajuste é feito por
        // sessão via RepriceProvisionedExpenseAsync (o app roteia pro fluxo certo).
        if (request.ExpectedAmount.HasValue && expense.OccurrencesTotal is not null)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "provisioned_expense",
                "Despesa provisionada é reajustada por sessão, não pelo valor total.");
        }

        // Account ownership check
        if (request.PaidFromAccountId.HasValue)
        {
            var accountOwnedByUser = await _db.CheckingAccounts
                .AnyAsync(a => a.Id == request.PaidFromAccountId.Value && a.UserId == userId, ct);
            if (!accountOwnedByUser)
            {
                return OperationResult.Fail<ExpenseResponse>(
                    "invalid_account",
                    "Conta não existe ou não pertence ao usuário.");
            }
            expense.PaidFromAccountId = request.PaidFromAccountId.Value;
        }

        // Category existence check
        if (request.CategoryId.HasValue && request.CategoryId.Value != expense.CategoryId)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, ct);
            if (!categoryExists)
            {
                return OperationResult.Fail<ExpenseResponse>(
                    "invalid_category", "Categoria não existe.");
            }
            expense.CategoryId = request.CategoryId.Value;
        }

        if (request.DueDate.HasValue)
        {
            expense.DueDate = request.DueDate.Value;
        }
        if (request.Label is not null)
        {
            expense.Label = request.Label.Trim();
        }
        if (request.ExpectedAmount.HasValue)
        {
            expense.ExpectedAmount = request.ExpectedAmount.Value;
        }
        if (request.ActualAmount.HasValue)
        {
            expense.ActualAmount = request.ActualAmount.Value;
        }
        if (request.PaidDate.HasValue)
        {
            expense.PaidDate = request.PaidDate.Value;
        }
        if (request.Notes is not null)
        {
            expense.Notes = NormalizeOptional(request.Notes);
        }

        // Status: se cliente marca como Paid, auto-preenche paid_date e actual_amount se não vieram
        if (request.Status.HasValue)
        {
            expense.Status = request.Status.Value;
            if (request.Status.Value == ExpenseStatus.Paid)
            {
                expense.PaidDate ??= DateOnly.FromDateTime(DateTime.UtcNow.Date);
                expense.ActualAmount ??= expense.ExpectedAmount;
            }
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.PaidFromAccount)
            .FirstAsync(e => e.Id == id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(updated, today));
    }

    /// <summary>
    /// Dá baixa em UMA ocorrência (semana) de uma despesa provisionada:
    /// incrementa OccurrencesPaid, soma o valor a PaidToDate, e quando a
    /// última ocorrência é paga, transiciona pra Paid (ActualAmount = PaidToDate).
    /// O ExpectedAmount cheio permanece reservado no remaining até quitar tudo
    /// (a matemática vive no HomeService: reserva = ExpectedAmount − PaidToDate).
    /// </summary>
    public async Task<OperationResult<ExpenseResponse>> PayOccurrenceAsync(
        Guid userId, Guid id, PayOccurrenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var expense = await _db.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (expense is null)
        {
            return OperationResult.Fail<ExpenseResponse>("not_found", "Despesa não encontrada.");
        }

        if (expense.OccurrencesTotal is not { } total)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "not_provisioned", "Despesa não é provisionada (sem ocorrências).");
        }

        if (expense.OccurrencesPaid >= total)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "already_settled", "Todas as ocorrências já foram quitadas.");
        }

        var amount = request.Amount ?? expense.OccurrenceAmount ?? 0m;
        if (amount <= 0m)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "validation_error", "Valor da baixa deve ser positivo.");
        }

        if (request.PaidFromAccountId.HasValue)
        {
            var accountOwnedByUser = await _db.CheckingAccounts
                .AnyAsync(a => a.Id == request.PaidFromAccountId.Value && a.UserId == userId, ct);
            if (!accountOwnedByUser)
            {
                return OperationResult.Fail<ExpenseResponse>(
                    "invalid_account", "Conta não existe ou não pertence ao usuário.");
            }
            expense.PaidFromAccountId = request.PaidFromAccountId.Value;
        }

        expense.OccurrencesPaid += 1;
        expense.PaidToDate += amount;
        expense.PaidDate = request.PaidDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        // Última ocorrência → despesa quitada.
        if (expense.OccurrencesPaid >= total)
        {
            expense.Status = ExpenseStatus.Paid;
            expense.ActualAmount = expense.PaidToDate;
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.PaidFromAccount)
            .FirstAsync(e => e.Id == id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(updated, today));
    }

    /// <summary>
    /// Reajusta o valor POR SESSÃO (OccurrenceAmount) de uma despesa provisionada.
    /// This → só esta ocorrência. ThisAndFollowing → esta + as futuras não-pagas do
    /// mesmo template, e atualiza o DefaultAmount do template pra ciclos futuros.
    /// O total (ExpectedAmount) de cada ocorrência recalcula = valor × OccurrencesTotal
    /// dela. PaidToDate/OccurrencesPaid ficam — a reserva se ajusta sozinha.
    /// </summary>
    public async Task<OperationResult<ExpenseResponse>> RepriceProvisionedExpenseAsync(
        Guid userId, Guid id, RepriceProvisionedExpenseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _repriceValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var expense = await _db.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (expense is null)
        {
            return OperationResult.Fail<ExpenseResponse>("not_found", "Despesa não encontrada.");
        }

        if (expense.OccurrencesTotal is null)
        {
            return OperationResult.Fail<ExpenseResponse>(
                "not_provisioned", "Despesa não é provisionada (sem ocorrências).");
        }

        if (request.Scope == RecurrenceScope.ThisAndFollowing && expense.TemplateId is { } templateId)
        {
            var siblings = await _db.Expenses
                .Where(e => e.UserId == userId && e.TemplateId == templateId)
                .ToListAsync(ct);

            var idsToReprice = OccurrencesToReprice(siblings, expense, request.Scope);
            foreach (var occurrence in siblings.Where(e => idsToReprice.Contains(e.Id)))
            {
                RepriceOccurrence(occurrence, request.Amount);
            }

            var template = await _db.ExpenseRecurrences
                .FirstOrDefaultAsync(r => r.Id == templateId && r.UserId == userId, ct);
            if (template is not null)
            {
                template.DefaultAmount = request.Amount;
            }
        }
        else
        {
            RepriceOccurrence(expense, request.Amount);
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.PaidFromAccount)
            .FirstAsync(e => e.Id == id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(updated, today));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, RecurrenceScope scope = RecurrenceScope.This, CancellationToken ct = default)
    {
        var expense = await _db.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (expense is null)
        {
            return OperationResult.Fail<bool>("not_found", "Despesa não encontrada.");
        }

        // ThisAndFollowing só faz sentido em despesa recorrente (com TemplateId):
        // apaga esta + as ocorrências seguintes ainda não pagas E encerra o template
        // (IsActive = false) pra parar a geração futura. Sem TemplateId, cai no This.
        if (scope == RecurrenceScope.ThisAndFollowing && expense.TemplateId is { } templateId)
        {
            var siblings = await _db.Expenses
                .Where(e => e.UserId == userId && e.TemplateId == templateId)
                .ToListAsync(ct);

            var idsToDelete = OccurrencesToDelete(siblings, expense, scope);
            var toDelete = siblings.Where(e => idsToDelete.Contains(e.Id));
            _db.Expenses.RemoveRange(toDelete);

            var template = await _db.ExpenseRecurrences
                .FirstOrDefaultAsync(r => r.Id == templateId && r.UserId == userId, ct);
            if (template is not null)
            {
                template.IsActive = false;
            }
        }
        else
        {
            _db.Expenses.Remove(expense);
        }

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    // ----- helpers -----

    /// <summary>
    /// Seleciona as ocorrências a excluir de um template, dado o escopo:
    /// <list type="bullet">
    /// <item><see cref="RecurrenceScope.This"/> → apenas a ocorrência-alvo.</item>
    /// <item><see cref="RecurrenceScope.ThisAndFollowing"/> → a alvo + as com
    /// <c>DueDate &gt;= target.DueDate</c> que ainda NÃO estão pagas (uma
    /// ocorrência futura já quitada permanece, preservando o histórico).</item>
    /// </list>
    /// Puro (sem DB) — testado como ComputeExpenseBuckets.
    /// </summary>
    internal static IReadOnlyCollection<Guid> OccurrencesToDelete(
        IReadOnlyCollection<Expense> templateOccurrences, Expense target, RecurrenceScope scope)
        => OccurrencesInScope(templateOccurrences, target, scope);

    /// <summary>Ocorrências que recebem o novo valor num reajuste, dado o escopo.
    /// Mesma regra do <see cref="OccurrencesToDelete"/>.</summary>
    internal static IReadOnlyCollection<Guid> OccurrencesToReprice(
        IReadOnlyCollection<Expense> templateOccurrences, Expense target, RecurrenceScope scope)
        => OccurrencesInScope(templateOccurrences, target, scope);

    // Núcleo compartilhado por delete e reprice: dado o escopo, retorna os ids das
    // ocorrências afetadas. This → só a alvo. ThisAndFollowing → a alvo + as com
    // DueDate >= alvo que ainda não estão pagas (Paid ficam de fora).
    private static IReadOnlyCollection<Guid> OccurrencesInScope(
        IReadOnlyCollection<Expense> templateOccurrences, Expense target, RecurrenceScope scope)
    {
        ArgumentNullException.ThrowIfNull(templateOccurrences);
        ArgumentNullException.ThrowIfNull(target);

        if (scope == RecurrenceScope.This)
        {
            return new[] { target.Id };
        }

        var ids = new HashSet<Guid> { target.Id };
        foreach (var occurrence in templateOccurrences)
        {
            if (occurrence.DueDate >= target.DueDate && occurrence.Status != ExpenseStatus.Paid)
            {
                ids.Add(occurrence.Id);
            }
        }
        return ids;
    }

    /// <summary>
    /// Aplica o novo valor POR SESSÃO a uma ocorrência provisionada: OccurrenceAmount
    /// recebe o valor e ExpectedAmount (total do ciclo) recalcula = valor ×
    /// OccurrencesTotal dela (ciclos de 4 ou 5 semanas usam o próprio total).
    /// PaidToDate/OccurrencesPaid ficam — a reserva (ExpectedAmount − PaidToDate)
    /// se ajusta sozinha. Puro (sem DB) — testado.
    /// </summary>
    internal static void RepriceOccurrence(Expense occurrence, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        occurrence.OccurrenceAmount = amount;
        occurrence.ExpectedAmount = amount * (occurrence.OccurrencesTotal ?? 1);
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

    private static ExpenseResponse MapToResponse(Expense e, DateOnly today) =>
        new(
            e.Id,
            e.DueDate,
            e.Label,
            e.ExpectedAmount,
            e.ActualAmount,
            e.ComputeDisplayStatus(today),
            e.PaidDate,
            e.PaidFromAccountId,
            e.PaidFromAccount?.BankName,
            e.CategoryId,
            e.Category.NamePt,
            e.LinkedCardStatementId,
            e.Notes,
            e.OccurrenceAmount,
            e.OccurrencesTotal,
            e.OccurrencesPaid,
            e.PaidToDate,
            e.CreatedAt,
            e.UpdatedAt);
}
