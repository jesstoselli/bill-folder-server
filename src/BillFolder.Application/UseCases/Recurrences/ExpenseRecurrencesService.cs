using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Recurrences;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Recurrences;

public class ExpenseRecurrencesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateExpenseRecurrenceRequest> _createValidator;
    private readonly IValidator<UpdateExpenseRecurrenceRequest> _updateValidator;

    public ExpenseRecurrencesService(
        IApplicationDbContext db,
        IValidator<CreateExpenseRecurrenceRequest> createValidator,
        IValidator<UpdateExpenseRecurrenceRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<ExpenseRecurrenceResponse>> ListAsync(
        Guid userId, bool? activeOnly, CancellationToken ct = default)
    {
        var query = _db.ExpenseRecurrences
            .AsNoTracking()
            .Where(r => r.UserId == userId);

        if (activeOnly == true)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderBy(r => r.DefaultLabel)
            .Include(r => r.DefaultCategory)
            .Select(r => new ExpenseRecurrenceResponse(
                r.Id, r.DefaultLabel, r.DefaultAmount,
                r.DefaultCategoryId, r.DefaultCategory.NamePt,
                r.DueDay, r.StartDate, r.EndDate, r.IsActive,
                r.CreatedAt, r.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<ExpenseRecurrenceResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var rec = await _db.ExpenseRecurrences
            .AsNoTracking()
            .Include(r => r.DefaultCategory)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        return rec is null
            ? OperationResult.Fail<ExpenseRecurrenceResponse>("not_found", "Recorrência não encontrada.")
            : OperationResult.Ok(MapToResponse(rec));
    }

    public async Task<OperationResult<ExpenseRecurrenceResponse>> CreateAsync(
        Guid userId, CreateExpenseRecurrenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<ExpenseRecurrenceResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == request.DefaultCategoryId, ct);
        if (!categoryExists)
        {
            return OperationResult.Fail<ExpenseRecurrenceResponse>(
                "invalid_category", "Categoria não existe.");
        }

        var rec = new ExpenseRecurrence
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DefaultLabel = request.DefaultLabel.Trim(),
            DefaultAmount = request.DefaultAmount,
            DefaultCategoryId = request.DefaultCategoryId,
            DueDay = request.DueDay,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
        };

        _db.ExpenseRecurrences.Add(rec);
        await _db.SaveChangesAsync(ct);

        var created = await _db.ExpenseRecurrences
            .AsNoTracking()
            .Include(r => r.DefaultCategory)
            .FirstAsync(r => r.Id == rec.Id, ct);

        return OperationResult.Ok(MapToResponse(created));
    }

    public async Task<OperationResult<ExpenseRecurrenceResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateExpenseRecurrenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<ExpenseRecurrenceResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var rec = await _db.ExpenseRecurrences
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (rec is null)
        {
            return OperationResult.Fail<ExpenseRecurrenceResponse>(
                "not_found", "Recorrência não encontrada.");
        }

        // Cross-field check
        var newStart = request.StartDate ?? rec.StartDate;
        var newEnd = request.EndDate ?? rec.EndDate;
        if (newEnd.HasValue && newEnd.Value < newStart)
        {
            return OperationResult.Fail<ExpenseRecurrenceResponse>(
                "validation_error",
                "Data de fim deve ser igual ou posterior à de início.");
        }

        if (request.DefaultCategoryId.HasValue && request.DefaultCategoryId.Value != rec.DefaultCategoryId)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.DefaultCategoryId.Value, ct);
            if (!categoryExists)
            {
                return OperationResult.Fail<ExpenseRecurrenceResponse>(
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
        if (request.DueDay.HasValue)
        {
            rec.DueDay = request.DueDay.Value;
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

        var updated = await _db.ExpenseRecurrences
            .AsNoTracking()
            .Include(r => r.DefaultCategory)
            .FirstAsync(r => r.Id == id, ct);

        return OperationResult.Ok(MapToResponse(updated));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var rec = await _db.ExpenseRecurrences
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

        if (rec is null)
        {
            return OperationResult.Fail<bool>("not_found", "Recorrência não encontrada.");
        }

        // FK em expenses.template_id é ON DELETE SET NULL — instances ficam sem template
        _db.ExpenseRecurrences.Remove(rec);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    private static ExpenseRecurrenceResponse MapToResponse(ExpenseRecurrence r) =>
        new(r.Id, r.DefaultLabel, r.DefaultAmount,
            r.DefaultCategoryId, r.DefaultCategory.NamePt,
            r.DueDay, r.StartDate, r.EndDate, r.IsActive,
            r.CreatedAt, r.UpdatedAt);
}
