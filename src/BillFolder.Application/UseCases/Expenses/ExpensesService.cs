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

    public ExpensesService(
        IApplicationDbContext db,
        IValidator<CreateExpenseRequest> createValidator,
        IValidator<UpdateExpenseRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var expense = await _db.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (expense is null)
        {
            return OperationResult.Fail<bool>("not_found", "Despesa não encontrada.");
        }

        _db.Expenses.Remove(expense);
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
            e.CreatedAt,
            e.UpdatedAt);
}
