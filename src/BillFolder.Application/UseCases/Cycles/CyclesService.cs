using System.Globalization;
using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Cycles;
using BillFolder.Application.UseCases.Incomes;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Cycles;

public class CyclesService
{
    /// <summary>
    /// Quantos ciclos gerar automaticamente à frente do ciclo que o user
    /// acabou de criar (ou do último existente, no safety-net). 12 = 1 ano
    /// de janela pra planejamento sem que o user precise criar manualmente.
    /// </summary>
    private const int RollingWindowSize = 12;

    private static readonly CultureInfo PtBrCulture = new("pt-BR");

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

        if (current is null)
        {
            // Safety-net: se o user tem histórico mas a janela de 12
            // ciclos gerados no last CreateAsync expirou (ficou meses
            // sem abrir o app), gera outra janela e re-tenta. Zero UX
            // visível — só destrava o app.
            var lastCycle = await _db.Cycles
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.EndDate)
                .FirstOrDefaultAsync(ct);

            if (lastCycle is not null && lastCycle.EndDate < today)
            {
                await GenerateForwardCyclesAsync(userId, lastCycle, RollingWindowSize, ct);
                await _db.SaveChangesAsync(ct);

                current = await _db.Cycles
                    .AsNoTracking()
                    .Where(c => c.UserId == userId
                             && c.StartDate <= today
                             && c.EndDate >= today)
                    .FirstOrDefaultAsync(ct);
            }
        }

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

        // Materializa entries de todas as sources ativas cujo range cobre o
        // novo ciclo — resolve o caso "source cadastrada antes de existir
        // ciclo". Complementa IncomeSourcesService que resolve o inverso
        // (source cadastrada depois do ciclo).
        await IncomeSourceExpansion.ExpandForCycleAsync(_db, cycle, ct);

        // Rolling window: gera 11 ciclos consecutivos à frente pra o user
        // ter 12 meses de janela sem precisar criar cada um manualmente. Se
        // ele deixar o app parado por 12 meses, o safety-net do
        // GetCurrentAsync gera outra janela.
        await GenerateForwardCyclesAsync(userId, cycle, RollingWindowSize - 1, ct);

        await _db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return OperationResult.Ok(MapToResponse(cycle, today));
    }

    // ========================================================================
    // Rolling window helpers
    // ========================================================================

    /// <summary>
    /// Gera <paramref name="count"/> ciclos consecutivos à frente do
    /// <paramref name="seed"/>. Cada um é marcado com
    /// IsRecurrenceGenerated=true (diferencia de ciclos criados
    /// manualmente pelo user), e dispara ExpandForCycleAsync pra
    /// materializar income entries das sources ativas.
    ///
    /// Idempotente: antes de criar cada ciclo, checa se já existe um com
    /// aquela StartDate. Se sim, para a cadeia (assume que ciclos
    /// posteriores já foram gerados numa execução anterior).
    ///
    /// Adiciona ao change tracker; caller é responsável pelo SaveChanges.
    /// </summary>
    private async Task<int> GenerateForwardCyclesAsync(
        Guid userId,
        Cycle seed,
        int count,
        CancellationToken ct)
    {
        var previous = seed;
        var generated = 0;

        for (var i = 0; i < count; i++)
        {
            var (nextStart, nextEnd) = ComputeNextCyclePeriod(previous);

            // Idempotência: se já existe um ciclo com essa startDate, para.
            // Também prevê race com o unique constraint (user_id, start_date).
            var exists = await _db.Cycles.AnyAsync(
                c => c.UserId == userId && c.StartDate == nextStart, ct);
            if (exists)
            {
                break;
            }

            var next = new Cycle
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                StartDate = nextStart,
                EndDate = nextEnd,
                Label = GenerateLabel(nextStart),
                IsRecurrenceGenerated = true,
            };
            _db.Cycles.Add(next);

            await IncomeSourceExpansion.ExpandForCycleAsync(_db, next, ct);

            previous = next;
            generated++;
        }

        return generated;
    }

    /// <summary>
    /// Regra de "próximo ciclo": start = end anterior + 1 dia. Pra o end,
    /// detecta o padrão "mês calendário" (1 ao último do mês) e replica.
    /// Se for custom (ex: 25→25), mantém mesma largura via +1 mês -1 dia.
    /// </summary>
    private static (DateOnly nextStart, DateOnly nextEnd) ComputeNextCyclePeriod(Cycle previous)
    {
        var nextStart = previous.EndDate.AddDays(1);

        var isCalendarMonth = previous.StartDate.Day == 1
            && previous.EndDate.Day == DateTime.DaysInMonth(
                previous.EndDate.Year, previous.EndDate.Month);

        var nextEnd = isCalendarMonth
            ? new DateOnly(
                nextStart.Year,
                nextStart.Month,
                DateTime.DaysInMonth(nextStart.Year, nextStart.Month))
            : nextStart.AddMonths(1).AddDays(-1);

        return (nextStart, nextEnd);
    }

    /// <summary>
    /// Label default no formato "mes/ano" em pt-BR minúsculo — igual à
    /// convenção do defaultLabel() no CreateCycleViewModel do app.
    /// Ex: "julho/2026".
    /// </summary>
    private static string GenerateLabel(DateOnly cycleStart)
    {
        var monthName = PtBrCulture.DateTimeFormat.GetMonthName(cycleStart.Month);
        return $"{PtBrCulture.TextInfo.ToLower(monthName)}/{cycleStart.Year}";
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
