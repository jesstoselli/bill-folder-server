using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.DailyExpenses;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.DailyExpenses;

public class DailyExpensesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateDailyExpenseRequest> _createValidator;
    private readonly IValidator<UpdateDailyExpenseRequest> _updateValidator;

    public DailyExpensesService(
        IApplicationDbContext db,
        IValidator<CreateDailyExpenseRequest> createValidator,
        IValidator<UpdateDailyExpenseRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<DailyExpenseResponse>> ListAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        CancellationToken ct = default)
    {
        var query = _db.DailyExpenses
            .AsNoTracking()
            .Where(de => de.UserId == userId);

        if (from.HasValue)
        {
            query = query.Where(de => de.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(de => de.Date <= to.Value);
        }
        if (categoryId.HasValue)
        {
            query = query.Where(de => de.CategoryId == categoryId.Value);
        }

        return await query
            .OrderByDescending(de => de.Date)
            .ThenByDescending(de => de.CreatedAt)
            .Select(de => new DailyExpenseResponse(
                de.Id,
                de.Date,
                de.Label,
                de.Amount,
                de.CategoryId,
                de.Category.NamePt,
                de.AccountId,
                de.Account.BankName,
                de.Notes,
                de.CreatedAt,
                de.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<DailyExpenseResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var de = await _db.DailyExpenses
            .AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Account)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);

        return de is null
            ? OperationResult.Fail<DailyExpenseResponse>("not_found", "Despesa não encontrada.")
            : OperationResult.Ok(MapToResponse(de));
    }

    public async Task<OperationResult<DailyExpenseResponse>> CreateAsync(
        Guid userId, CreateDailyExpenseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<DailyExpenseResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        // Account precisa pertencer ao user (segurança crítica)
        var accountOwnedByUser = await _db.CheckingAccounts
            .AnyAsync(a => a.Id == request.AccountId && a.UserId == userId, ct);
        if (!accountOwnedByUser)
        {
            return OperationResult.Fail<DailyExpenseResponse>(
                "invalid_account",
                "Conta corrente não existe ou não pertence ao usuário.");
        }

        // Category precisa existir (categorias são globais, sem user_id)
        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
        {
            return OperationResult.Fail<DailyExpenseResponse>(
                "invalid_category", "Categoria não existe.");
        }

        var de = new DailyExpense
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Date = request.Date,
            CategoryId = request.CategoryId,
            Label = request.Label.Trim(),
            Amount = request.Amount,
            AccountId = request.AccountId,
            Notes = NormalizeOptional(request.Notes),
        };

        _db.DailyExpenses.Add(de);
        await _db.SaveChangesAsync(ct);

        // Reload pra incluir Category + Account (pra mapear pro response com nomes)
        var created = await _db.DailyExpenses
            .AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Account)
            .FirstAsync(d => d.Id == de.Id, ct);

        return OperationResult.Ok(MapToResponse(created));
    }

    public async Task<OperationResult<DailyExpenseResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateDailyExpenseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<DailyExpenseResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var de = await _db.DailyExpenses
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);

        if (de is null)
        {
            return OperationResult.Fail<DailyExpenseResponse>(
                "not_found", "Despesa não encontrada.");
        }

        // Se mudou account, valida ownership
        if (request.AccountId.HasValue && request.AccountId.Value != de.AccountId)
        {
            var accountOwnedByUser = await _db.CheckingAccounts
                .AnyAsync(a => a.Id == request.AccountId.Value && a.UserId == userId, ct);
            if (!accountOwnedByUser)
            {
                return OperationResult.Fail<DailyExpenseResponse>(
                    "invalid_account",
                    "Conta corrente não existe ou não pertence ao usuário.");
            }
            de.AccountId = request.AccountId.Value;
        }

        // Se mudou category, valida existência
        if (request.CategoryId.HasValue && request.CategoryId.Value != de.CategoryId)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, ct);
            if (!categoryExists)
            {
                return OperationResult.Fail<DailyExpenseResponse>(
                    "invalid_category", "Categoria não existe.");
            }
            de.CategoryId = request.CategoryId.Value;
        }

        if (request.Date.HasValue)
        {
            de.Date = request.Date.Value;
        }
        if (request.Label is not null)
        {
            de.Label = request.Label.Trim();
        }
        if (request.Amount.HasValue)
        {
            de.Amount = request.Amount.Value;
        }
        if (request.Notes is not null)
        {
            // String vazia → null (limpar campo)
            de.Notes = NormalizeOptional(request.Notes);
        }

        await _db.SaveChangesAsync(ct);

        // Reload com navigations pro response
        var updated = await _db.DailyExpenses
            .AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Account)
            .FirstAsync(d => d.Id == id, ct);

        return OperationResult.Ok(MapToResponse(updated));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var de = await _db.DailyExpenses
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);

        if (de is null)
        {
            return OperationResult.Fail<bool>("not_found", "Despesa não encontrada.");
        }

        _db.DailyExpenses.Remove(de);
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

    private static DailyExpenseResponse MapToResponse(DailyExpense de) =>
        new(de.Id,
            de.Date,
            de.Label,
            de.Amount,
            de.CategoryId,
            de.Category.NamePt,
            de.AccountId,
            de.Account.BankName,
            de.Notes,
            de.CreatedAt,
            de.UpdatedAt);
}
