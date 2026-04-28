using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Incomes;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Incomes;

public class IncomeSourcesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateIncomeSourceRequest> _createValidator;
    private readonly IValidator<UpdateIncomeSourceRequest> _updateValidator;

    public IncomeSourcesService(
        IApplicationDbContext db,
        IValidator<CreateIncomeSourceRequest> createValidator,
        IValidator<UpdateIncomeSourceRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<IncomeSourceResponse>> ListAsync(
        Guid userId, bool? activeOnly, CancellationToken ct = default)
    {
        var query = _db.IncomeSources
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        if (activeOnly == true)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.Origin)
            .Select(s => new IncomeSourceResponse(
                s.Id, s.Origin, s.OriginType, s.DefaultAmount, s.ExpectedDay,
                s.StartDate, s.EndDate, s.IsActive, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<IncomeSourceResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var source = await _db.IncomeSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        return source is null
            ? OperationResult.Fail<IncomeSourceResponse>("not_found", "Fonte de renda não encontrada.")
            : OperationResult.Ok(MapToResponse(source));
    }

    public async Task<OperationResult<IncomeSourceResponse>> CreateAsync(
        Guid userId, CreateIncomeSourceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<IncomeSourceResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var source = new IncomeSource
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Origin = request.Origin.Trim(),
            OriginType = request.OriginType,
            DefaultAmount = request.DefaultAmount,
            ExpectedDay = request.ExpectedDay,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
        };

        _db.IncomeSources.Add(source);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(source));
    }

    public async Task<OperationResult<IncomeSourceResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateIncomeSourceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<IncomeSourceResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var source = await _db.IncomeSources
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (source is null)
        {
            return OperationResult.Fail<IncomeSourceResponse>(
                "not_found", "Fonte de renda não encontrada.");
        }

        // Cross-field: end_date >= start_date (calculando contra valores efetivos pós-update)
        var newStart = request.StartDate ?? source.StartDate;
        var newEnd = request.EndDate ?? source.EndDate;
        if (newEnd.HasValue && newEnd.Value < newStart)
        {
            return OperationResult.Fail<IncomeSourceResponse>(
                "validation_error",
                "Data de fim deve ser igual ou posterior à data de início.");
        }

        if (request.Origin is not null)
        {
            source.Origin = request.Origin.Trim();
        }
        if (request.OriginType.HasValue)
        {
            source.OriginType = request.OriginType.Value;
        }
        if (request.DefaultAmount.HasValue)
        {
            source.DefaultAmount = request.DefaultAmount.Value;
        }
        if (request.ExpectedDay.HasValue)
        {
            source.ExpectedDay = request.ExpectedDay.Value;
        }
        if (request.StartDate.HasValue)
        {
            source.StartDate = request.StartDate.Value;
        }
        if (request.EndDate.HasValue)
        {
            source.EndDate = request.EndDate.Value;
        }
        if (request.IsActive.HasValue)
        {
            source.IsActive = request.IsActive.Value;
        }

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(source));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var source = await _db.IncomeSources
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (source is null)
        {
            return OperationResult.Fail<bool>("not_found", "Fonte de renda não encontrada.");
        }

        // FK em income_entries.source_id é ON DELETE SET NULL — entries históricas
        // ficam mas perdem referência ao template. Esse é o comportamento esperado.
        _db.IncomeSources.Remove(source);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    private static IncomeSourceResponse MapToResponse(IncomeSource s) =>
        new(s.Id, s.Origin, s.OriginType, s.DefaultAmount, s.ExpectedDay,
            s.StartDate, s.EndDate, s.IsActive, s.CreatedAt, s.UpdatedAt);
}
