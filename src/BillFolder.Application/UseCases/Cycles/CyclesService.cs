using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Cycles;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Cycles;

public class CyclesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateCycleRequest> _createValidator;
    private readonly IValidator<UpdateCycleRequest> _updateValidator;

    public CyclesService(
        IApplicationDbContext db,
        IValidator<CreateCycleRequest> createValidator,
        IValidator<UpdateCycleRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<CycleResponse>> ListAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var query = _db.Cycles
            .AsNoTracking()
            .Where(c => c.UserId == userId);

        if (from.HasValue)
        {
            query = query.Where(c => c.EndDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(c => c.StartDate <= to.Value);
        }

        var cycles = await query
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);

        return cycles.Select(c => MapToResponse(c, today)).ToList();
    }

    public async Task<OperationResult<CycleResponse>> GetCurrentAsync(
        Guid userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var current = await _db.Cycles
            .AsNoTracking()
            .Where(c => c.UserId == userId
                     && c.StartDate <= today
                     && c.EndDate >= today)
            .FirstOrDefaultAsync(ct);

        return current is null
            ? OperationResult.Fail<CycleResponse>(
                "no_current_cycle",
                "Nenhum ciclo ativo cobre a data de hoje.")
            : OperationResult.Ok(MapToResponse(current, today));
    }

    public async Task<OperationResult<CycleResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var cycle = await _db.Cycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (cycle is null)
        {
            return OperationResult.Fail<CycleResponse>("not_found", "Ciclo não encontrado.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(cycle, today));
    }

    public async Task<OperationResult<CycleResponse>> CreateAsync(
        Guid userId, CreateCycleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CycleResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        // Pre-check do constraint UNIQUE (user_id, start_date) — UX melhor que cair
        // numa exception de constraint violation
        var duplicate = await _db.Cycles
            .AnyAsync(c => c.UserId == userId && c.StartDate == request.StartDate, ct);
        if (duplicate)
        {
            return OperationResult.Fail<CycleResponse>(
                "duplicate_start_date",
                "Já existe um ciclo com essa data de início.");
        }

        var cycle = new Cycle
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Label = request.Label.Trim(),
            IsRecurrenceGenerated = false,  // sempre false em create manual
        };

        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(cycle, today));
    }

    public async Task<OperationResult<CycleResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateCycleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CycleResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var cycle = await _db.Cycles
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (cycle is null)
        {
            return OperationResult.Fail<CycleResponse>("not_found", "Ciclo não encontrado.");
        }

        // Calcula novos valores antes de aplicar pra validar invariants cross-field
        var newStart = request.StartDate ?? cycle.StartDate;
        var newEnd = request.EndDate ?? cycle.EndDate;

        if (newStart >= newEnd)
        {
            return OperationResult.Fail<CycleResponse>(
                "validation_error",
                "Data de início deve ser anterior à data de fim.");
        }

        // Se mudou start_date, checa duplicata
        if (request.StartDate.HasValue && request.StartDate.Value != cycle.StartDate)
        {
            var duplicate = await _db.Cycles
                .AnyAsync(c => c.UserId == userId
                            && c.StartDate == request.StartDate.Value
                            && c.Id != id,
                          ct);
            if (duplicate)
            {
                return OperationResult.Fail<CycleResponse>(
                    "duplicate_start_date",
                    "Já existe um ciclo com essa data de início.");
            }
            cycle.StartDate = request.StartDate.Value;
        }

        if (request.EndDate.HasValue)
        {
            cycle.EndDate = request.EndDate.Value;
        }
        if (request.Label is not null)
        {
            cycle.Label = request.Label.Trim();
        }

        await _db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(cycle, today));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var cycle = await _db.Cycles
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (cycle is null)
        {
            return OperationResult.Fail<bool>("not_found", "Ciclo não encontrado.");
        }

        _db.Cycles.Remove(cycle);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    // ----- helpers -----

    private static CycleResponse MapToResponse(Cycle c, DateOnly today) =>
        new(
            c.Id,
            c.StartDate,
            c.EndDate,
            c.Label,
            c.IsRecurrenceGenerated,
            IsCurrent: c.StartDate <= today && c.EndDate >= today,
            c.CreatedAt,
            c.UpdatedAt);
}
