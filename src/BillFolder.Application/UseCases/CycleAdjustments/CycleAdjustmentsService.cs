using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.CycleAdjustments;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.CycleAdjustments;

public class CycleAdjustmentsService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateCycleAdjustmentRequest> _createValidator;
    private readonly IValidator<UpdateCycleAdjustmentRequest> _updateValidator;

    public CycleAdjustmentsService(
        IApplicationDbContext db,
        IValidator<CreateCycleAdjustmentRequest> createValidator,
        IValidator<UpdateCycleAdjustmentRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<CycleAdjustmentResponse>> ListAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        CycleAdjustmentType? type,
        CancellationToken ct = default)
    {
        var query = _db.CycleAdjustments
            .AsNoTracking()
            .Where(a => a.UserId == userId);

        if (from.HasValue)
        {
            query = query.Where(a => a.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(a => a.Date <= to.Value);
        }
        if (type.HasValue)
        {
            query = query.Where(a => a.Type == type.Value);
        }

        return await query
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new CycleAdjustmentResponse(
                a.Id, a.Type, a.Label, a.Amount, a.Date,
                a.SourceSavingsTransactionId, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<CycleAdjustmentResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var adjustment = await _db.CycleAdjustments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        return adjustment is null
            ? OperationResult.Fail<CycleAdjustmentResponse>("not_found", "Ajuste não encontrado.")
            : OperationResult.Ok(MapToResponse(adjustment));
    }

    public async Task<OperationResult<CycleAdjustmentResponse>> CreateAsync(
        Guid userId, CreateCycleAdjustmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CycleAdjustmentResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        // Se source_savings_transaction_id foi enviado, valida ownership
        if (request.SourceSavingsTransactionId.HasValue)
        {
            var sourceOwnedByUser = await _db.SavingsTransactions
                .AnyAsync(t => t.Id == request.SourceSavingsTransactionId.Value
                            && t.UserId == userId, ct);
            if (!sourceOwnedByUser)
            {
                return OperationResult.Fail<CycleAdjustmentResponse>(
                    "invalid_source_transaction",
                    "Transação de poupança vinculada não existe ou não pertence ao usuário.");
            }
        }

        var adjustment = new CycleAdjustment
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Type = request.Type,
            Label = request.Label.Trim(),
            Amount = request.Amount,
            Date = request.Date,
            SourceSavingsTransactionId = request.SourceSavingsTransactionId,
        };

        _db.CycleAdjustments.Add(adjustment);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(adjustment));
    }

    public async Task<OperationResult<CycleAdjustmentResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateCycleAdjustmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CycleAdjustmentResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var adjustment = await _db.CycleAdjustments
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (adjustment is null)
        {
            return OperationResult.Fail<CycleAdjustmentResponse>(
                "not_found", "Ajuste não encontrado.");
        }

        if (request.SourceSavingsTransactionId.HasValue
            && request.SourceSavingsTransactionId.Value != adjustment.SourceSavingsTransactionId)
        {
            var sourceOwnedByUser = await _db.SavingsTransactions
                .AnyAsync(t => t.Id == request.SourceSavingsTransactionId.Value
                            && t.UserId == userId, ct);
            if (!sourceOwnedByUser)
            {
                return OperationResult.Fail<CycleAdjustmentResponse>(
                    "invalid_source_transaction",
                    "Transação de poupança vinculada não existe ou não pertence ao usuário.");
            }
            adjustment.SourceSavingsTransactionId = request.SourceSavingsTransactionId.Value;
        }

        if (request.Type.HasValue)
        {
            adjustment.Type = request.Type.Value;
        }
        if (request.Label is not null)
        {
            adjustment.Label = request.Label.Trim();
        }
        if (request.Amount.HasValue)
        {
            adjustment.Amount = request.Amount.Value;
        }
        if (request.Date.HasValue)
        {
            adjustment.Date = request.Date.Value;
        }

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(adjustment));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var adjustment = await _db.CycleAdjustments
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (adjustment is null)
        {
            return OperationResult.Fail<bool>("not_found", "Ajuste não encontrado.");
        }

        _db.CycleAdjustments.Remove(adjustment);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    private static CycleAdjustmentResponse MapToResponse(CycleAdjustment a) =>
        new(a.Id, a.Type, a.Label, a.Amount, a.Date,
            a.SourceSavingsTransactionId, a.CreatedAt, a.UpdatedAt);
}
