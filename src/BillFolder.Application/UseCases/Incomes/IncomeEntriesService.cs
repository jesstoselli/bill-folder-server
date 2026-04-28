using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Incomes;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Incomes;

public class IncomeEntriesService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateIncomeEntryRequest> _createValidator;
    private readonly IValidator<UpdateIncomeEntryRequest> _updateValidator;

    public IncomeEntriesService(
        IApplicationDbContext db,
        IValidator<CreateIncomeEntryRequest> createValidator,
        IValidator<UpdateIncomeEntryRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<IncomeEntryResponse>> ListAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        IncomeStatus? status,
        Guid? sourceId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var query = _db.IncomeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId);

        if (from.HasValue)
        {
            query = query.Where(e => e.ExpectedDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(e => e.ExpectedDate <= to.Value);
        }
        if (sourceId.HasValue)
        {
            query = query.Where(e => e.SourceId == sourceId.Value);
        }

        // Filtro de status considera "late" computado (igual overdue dos expenses)
        if (status.HasValue)
        {
            query = status.Value switch
            {
                IncomeStatus.Expected => query.Where(e => e.Status == IncomeStatus.Expected
                                                       && e.ExpectedDate >= today),
                IncomeStatus.Late     => query.Where(e => e.Status == IncomeStatus.Late
                                                      || (e.Status == IncomeStatus.Expected
                                                          && e.ExpectedDate < today)),
                IncomeStatus.Received => query.Where(e => e.Status == IncomeStatus.Received),
                IncomeStatus.NotOccurred => query.Where(e => e.Status == IncomeStatus.NotOccurred),
                _ => query,
            };
        }

        var entries = await query
            .OrderBy(e => e.ExpectedDate)
            .ThenByDescending(e => e.CreatedAt)
            .Include(e => e.Source)
            .ToListAsync(ct);

        return entries.Select(e => MapToResponse(e, today)).ToList();
    }

    public async Task<OperationResult<IncomeEntryResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var entry = await _db.IncomeEntries
            .AsNoTracking()
            .Include(e => e.Source)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (entry is null)
        {
            return OperationResult.Fail<IncomeEntryResponse>("not_found", "Entrada de renda não encontrada.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(entry, today));
    }

    public async Task<OperationResult<IncomeEntryResponse>> CreateAsync(
        Guid userId, CreateIncomeEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<IncomeEntryResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        // Se source_id foi enviado, valida ownership
        if (request.SourceId.HasValue)
        {
            var sourceOwnedByUser = await _db.IncomeSources
                .AnyAsync(s => s.Id == request.SourceId.Value && s.UserId == userId, ct);
            if (!sourceOwnedByUser)
            {
                return OperationResult.Fail<IncomeEntryResponse>(
                    "invalid_source",
                    "Fonte de renda não existe ou não pertence ao usuário.");
            }
        }

        var entry = new IncomeEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SourceId = request.SourceId,
            ExpectedAmount = request.ExpectedAmount,
            ExpectedDate = request.ExpectedDate,
            Status = IncomeStatus.Expected,
            Notes = NormalizeOptional(request.Notes),
        };

        _db.IncomeEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        var created = await _db.IncomeEntries
            .AsNoTracking()
            .Include(e => e.Source)
            .FirstAsync(e => e.Id == entry.Id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(created, today));
    }

    public async Task<OperationResult<IncomeEntryResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateIncomeEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<IncomeEntryResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var entry = await _db.IncomeEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (entry is null)
        {
            return OperationResult.Fail<IncomeEntryResponse>(
                "not_found", "Entrada de renda não encontrada.");
        }

        // Source ownership check se mudou
        if (request.SourceId.HasValue && request.SourceId.Value != entry.SourceId)
        {
            var sourceOwnedByUser = await _db.IncomeSources
                .AnyAsync(s => s.Id == request.SourceId.Value && s.UserId == userId, ct);
            if (!sourceOwnedByUser)
            {
                return OperationResult.Fail<IncomeEntryResponse>(
                    "invalid_source",
                    "Fonte de renda não existe ou não pertence ao usuário.");
            }
            entry.SourceId = request.SourceId.Value;
        }

        if (request.ExpectedAmount.HasValue)
        {
            entry.ExpectedAmount = request.ExpectedAmount.Value;
        }
        if (request.ActualAmount.HasValue)
        {
            entry.ActualAmount = request.ActualAmount.Value;
        }
        if (request.ExpectedDate.HasValue)
        {
            entry.ExpectedDate = request.ExpectedDate.Value;
        }
        if (request.ActualDate.HasValue)
        {
            entry.ActualDate = request.ActualDate.Value;
        }
        if (request.Notes is not null)
        {
            entry.Notes = NormalizeOptional(request.Notes);
        }

        // Status: se cliente marca como Received, auto-fill actual_date e actual_amount
        if (request.Status.HasValue)
        {
            entry.Status = request.Status.Value;
            if (request.Status.Value == IncomeStatus.Received)
            {
                entry.ActualDate ??= DateOnly.FromDateTime(DateTime.UtcNow.Date);
                entry.ActualAmount ??= entry.ExpectedAmount;
            }
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.IncomeEntries
            .AsNoTracking()
            .Include(e => e.Source)
            .FirstAsync(e => e.Id == id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(updated, today));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var entry = await _db.IncomeEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

        if (entry is null)
        {
            return OperationResult.Fail<bool>("not_found", "Entrada de renda não encontrada.");
        }

        _db.IncomeEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    // ----- helpers -----

    private static IncomeStatus ComputeDisplayStatus(IncomeEntry e, DateOnly today) =>
        e.Status == IncomeStatus.Expected && e.ExpectedDate < today
            ? IncomeStatus.Late
            : e.Status;

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }
        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static IncomeEntryResponse MapToResponse(IncomeEntry e, DateOnly today) =>
        new(
            e.Id,
            e.SourceId,
            e.Source?.Origin,
            e.ExpectedAmount,
            e.ActualAmount,
            e.ExpectedDate,
            e.ActualDate,
            ComputeDisplayStatus(e, today),
            e.Notes,
            e.CreatedAt,
            e.UpdatedAt);
}
