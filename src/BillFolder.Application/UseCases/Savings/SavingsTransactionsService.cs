using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Savings;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Savings;

public class SavingsTransactionsService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateSavingsTransactionRequest> _createValidator;
    private readonly IValidator<UpdateSavingsTransactionRequest> _updateValidator;

    public SavingsTransactionsService(
        IApplicationDbContext db,
        IValidator<CreateSavingsTransactionRequest> createValidator,
        IValidator<UpdateSavingsTransactionRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<SavingsTransactionResponse>> ListAsync(
        Guid userId,
        Guid? savingsAccountId,
        DateOnly? from,
        DateOnly? to,
        SavingsTransactionType? type,
        CancellationToken ct = default)
    {
        var query = _db.SavingsTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        if (savingsAccountId.HasValue)
        {
            query = query.Where(t => t.SavingsAccountId == savingsAccountId.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(t => t.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(t => t.Date <= to.Value);
        }
        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new SavingsTransactionResponse(
                t.Id, t.SavingsAccountId, t.Type, t.Amount, t.Date,
                t.Label, t.LinkedTransactionId, t.CreatedAt, t.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<SavingsTransactionResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var tx = await _db.SavingsTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

        return tx is null
            ? OperationResult.Fail<SavingsTransactionResponse>("not_found", "Transação não encontrada.")
            : OperationResult.Ok(MapToResponse(tx));
    }

    public async Task<OperationResult<SavingsTransactionResponse>> CreateAsync(
        Guid userId, CreateSavingsTransactionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<SavingsTransactionResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        // Savings account precisa pertencer ao user
        var savingsOwnedByUser = await _db.SavingsAccounts
            .AnyAsync(s => s.Id == request.SavingsAccountId && s.UserId == userId, ct);
        if (!savingsOwnedByUser)
        {
            return OperationResult.Fail<SavingsTransactionResponse>(
                "invalid_savings_account",
                "Poupança não existe ou não pertence ao usuário.");
        }

        var tx = new SavingsTransaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SavingsAccountId = request.SavingsAccountId,
            Type = request.Type,
            Amount = request.Amount,
            Date = request.Date,
            Label = NormalizeOptional(request.Label),
            LinkedTransactionId = request.LinkedTransactionId,
        };

        _db.SavingsTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(tx));
    }

    public async Task<OperationResult<SavingsTransactionResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateSavingsTransactionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<SavingsTransactionResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var tx = await _db.SavingsTransactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

        if (tx is null)
        {
            return OperationResult.Fail<SavingsTransactionResponse>(
                "not_found", "Transação não encontrada.");
        }

        if (request.Type.HasValue)
        {
            tx.Type = request.Type.Value;
        }
        if (request.Amount.HasValue)
        {
            tx.Amount = request.Amount.Value;
        }
        if (request.Date.HasValue)
        {
            tx.Date = request.Date.Value;
        }
        if (request.Label is not null)
        {
            tx.Label = NormalizeOptional(request.Label);
        }
        if (request.LinkedTransactionId.HasValue)
        {
            tx.LinkedTransactionId = request.LinkedTransactionId.Value;
        }

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(tx));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var tx = await _db.SavingsTransactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

        if (tx is null)
        {
            return OperationResult.Fail<bool>("not_found", "Transação não encontrada.");
        }

        _db.SavingsTransactions.Remove(tx);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
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

    private static SavingsTransactionResponse MapToResponse(SavingsTransaction t) =>
        new(t.Id, t.SavingsAccountId, t.Type, t.Amount, t.Date,
            t.Label, t.LinkedTransactionId, t.CreatedAt, t.UpdatedAt);
}
