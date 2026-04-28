using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Recurrences;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Recurrences;

public class DailyExpenseRecurrencesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateDailyExpenseRecurrenceRequest> _createValidator;
    private readonly IValidator<UpdateDailyExpenseRecurrenceRequest> _updateValidator;

    public DailyExpenseRecurrencesService(
        IApplicationDbContext db,
        IValidator<CreateDailyExpenseRecurrenceRequest> createValidator,
        IValidator<UpdateDailyExpenseRecurrenceRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<DailyExpenseRecurrenceResponse>> ListAsync(
        Guid userId, bool? activeOnly, CancellationToken ct = default)
    {
        var query = _db.DailyExpenseRecurrences
            .AsNoTracking()
            .Where(r => r.UserId == userId);

        if (activeOnly == true)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderBy(r => r.DefaultLabel)
            .Include(r => r.DefaultCategory)
            .Include(r => r.DefaultAccount)
            .Select(r => new DailyExpenseRecurrenceResponse(
                r.Id, r.DefaultLabel, r.DefaultAmount,
                r.DefaultCategoryId, r.DefaultCategory.NamePt,
                r.DefaultAccountId, r.DefaultAccount.BankName,
                r.DayOfMonth, r.StartDate, r.EndDate, r.IsActive,
                r.CreatedAt, r.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<DailyExpenseRecurrenceResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var rec = await _db.DailyExpenseRecurrences
            .AsNoTracking()
            .Include(r => r.DefaultCategory)
            .Include(r => r.DefaultAccount)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        return rec is null
            ? OperationResult.Fail<DailyExpenseRecurrenceResponse>("not_found", "Recorrência não encontrada.")
            : OperationResult.Ok(MapToResponse(rec));
    }

    public async Task<OperationResult<DailyExpenseRecurrenceResponse>> CreateAsync(
        Guid userId, CreateDailyExpenseRecurrenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<DailyExpenseRecurrenceResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == request.DefaultCategoryId, ct);
        if (!categoryExists)
        {
            return OperationResult.Fail<DailyExpenseRecurrenceResponse>(
                "invalid_category", "Categoria não existe.");
        }

        var accountOwnedByUser = await _db.CheckingAccounts
            .AnyAsync(a => a.Id == request.DefaultAccountId && a.UserId == userId, ct);
        if (!accountOwnedByUser)
        {
            return OperationResult.Fail<DailyExpenseRecurrenceResponse>(
                "invalid_account",
                "Conta corrente não existe ou não pertence ao usuário.");
        }

        var rec = new DailyExpenseRecurrence
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DefaultLabel = request.DefaultLabel.Trim(),
            DefaultAmount = request.DefaultAmount,
            DefaultCategoryId = request.DefaultCategoryId,
            DefaultAccountId = request.DefaultAccountId,
            DayOfMonth = request.DayOfMonth,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
        };

        _db.DailyExpenseRecurrences.Add(rec);
        await _db.SaveChangesAsync(ct);

        var created = await _db.DailyExpenseRecurrences
            .AsNoTracking()
            .Include(r => r.DefaultCategory)
            .Include(r => r.DefaultAccount)
            .FirstAsync(r => r.Id == rec.Id, ct);

        return OperationResult.Ok(MapToResponse(created));
    }

    public async Task<OperationResult<DailyExpenseRecurrenceResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateDailyExpenseRecurrenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<DailyExpenseRecurrenceResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var rec = await _db.DailyExpenseRecurrences
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (rec is null)
        {
            return OperationResult.Fail<DailyExpenseRecurrenceResponse>(
                "not_found", "Recorrência não encontrada.");
        }

        var newStart = request.StartDate ?? rec.StartDate;
        var newEnd = request.EndDate ?? rec.EndDate;
        if (newEnd.HasValue && newEnd.Value < newStart)
        {
            return OperationResult.Fail<DailyExpenseRecurrenceResponse>(
                "validation_error",
                "Data de fim deve ser igual ou posterior à de início.");
        }

        if (request.DefaultCategoryId.HasValue && request.DefaultCategoryId.Value != rec.DefaultCategoryId)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.DefaultCategoryId.Value, ct);
            if (!categoryExists)
            {
                return OperationResult.Fail<DailyExpenseRecurrenceResponse>(
                    "invalid_category", "Categoria não existe.");
            }
            rec.DefaultCategoryId = request.DefaultCategoryId.Value;
        }

        if (request.DefaultAccountId.HasValue && request.DefaultAccountId.Value != rec.DefaultAccountId)
        {
            var accountOwnedByUser = await _db.CheckingAccounts
                .AnyAsync(a => a.Id == request.DefaultAccountId.Value && a.UserId == userId, ct);
            if (!accountOwnedByUser)
            {
                return OperationResult.Fail<DailyExpenseRecurrenceResponse>(
                    "invalid_account",
                    "Conta corrente não existe ou não pertence ao usuário.");
            }
            rec.DefaultAccountId = request.DefaultAccountId.Value;
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

        var updated = await _db.DailyExpenseRecurrences
            .AsNoTracking()
            .Include(r => r.DefaultCategory)
            .Include(r => r.DefaultAccount)
            .FirstAsync(r => r.Id == id, ct);

        return OperationResult.Ok(MapToResponse(updated));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var rec = await _db.DailyExpenseRecurrences
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (rec is null)
        {
            return OperationResult.Fail<bool>("not_found", "Recorrência não encontrada.");
        }

        _db.DailyExpenseRecurrences.Remove(rec);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    private static DailyExpenseRecurrenceResponse MapToResponse(DailyExpenseRecurrence r) =>
        new(r.Id, r.DefaultLabel, r.DefaultAmount,
            r.DefaultCategoryId, r.DefaultCategory.NamePt,
            r.DefaultAccountId, r.DefaultAccount.BankName,
            r.DayOfMonth, r.StartDate, r.EndDate, r.IsActive,
            r.CreatedAt, r.UpdatedAt);
}
