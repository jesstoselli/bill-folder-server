using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Incomes;

/// <summary>
/// Materialização de IncomeSources (templates recorrentes) em IncomeEntries
/// concretas dentro de um Cycle. Chamada em 2 pontos:
///
///   - IncomeSourcesService.CreateAsync: source nova → expande pra todos
///     os ciclos abertos do user cujo range intersecta com a source.
///   - CyclesService.CreateAsync: ciclo novo → expande todas as sources
///     ativas do user que cobrem o ciclo.
///
/// Idempotente: antes de criar uma IncomeEntry, checa se já existe uma
/// com o mesmo (UserId, SourceId, ExpectedDate). Isso protege contra
/// duplicação se a mesma chamada rodar 2x (ex: retry de request).
///
/// Convenção pra ExpectedDay > dias no mês: cap no último dia do mês
/// (ex: source com ExpectedDay=31 num fevereiro vira dia 28/29).
/// Alternativa "pular mês" é possível, mas caping é o que a maioria dos
/// apps de folha de pagamento no BR faz (salário do dia 30 cai no
/// último dia útil / último dia do mês).
/// </summary>
public static class IncomeSourceExpansion
{
    /// <summary>
    /// Cria IncomeEntries pra <paramref name="source"/> dentro de todos
    /// os cycles do user que se sobrepõem com [source.StartDate,
    /// source.EndDate ?? +infinito]. As entries são apenas ADICIONADAS
    /// ao change tracker — SaveChangesAsync é responsabilidade do caller.
    /// </summary>
    public static async Task ExpandForSourceAsync(
        IApplicationDbContext db,
        IncomeSource source,
        CancellationToken ct)
    {
        // Só sources ativas materializam entries. IsActive controla se
        // a recorrência está "ligada" — inativas ficam paradas até serem
        // reativadas via PATCH.
        if (!source.IsActive)
        {
            return;
        }

        // Range da source (endDate null → hoje + 5 anos como sentinel
        // conservador; ciclos além disso o user cria manualmente e a
        // expansão do CyclesService cuida).
        var sourceEnd = source.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date).AddYears(5);

        // Pega todos os ciclos do user cujo range intersecta o da source.
        // Interseção: cycle.EndDate >= source.StartDate && cycle.StartDate <= sourceEnd.
        var cycles = await db.Cycles
            .Where(c => c.UserId == source.UserId
                     && c.EndDate >= source.StartDate
                     && c.StartDate <= sourceEnd)
            .ToListAsync(ct);

        foreach (var cycle in cycles)
        {
            await MaterializeEntriesAsync(db, source, cycle, ct);
        }
    }

    /// <summary>
    /// Cria IncomeEntries pra <paramref name="cycle"/> considerando todas
    /// as IncomeSources ATIVAS do user cujo range da source cobre parte
    /// do ciclo. Complementa ExpandForSourceAsync — juntos os 2 métodos
    /// cobrem source-criada-antes-do-ciclo E source-criada-depois-do-ciclo.
    /// </summary>
    public static async Task ExpandForCycleAsync(
        IApplicationDbContext db,
        Cycle cycle,
        CancellationToken ct)
    {
        var sources = await db.IncomeSources
            .Where(s => s.UserId == cycle.UserId
                     && s.IsActive
                     && s.StartDate <= cycle.EndDate
                     && (s.EndDate == null || s.EndDate >= cycle.StartDate))
            .ToListAsync(ct);

        foreach (var source in sources)
        {
            await MaterializeEntriesAsync(db, source, cycle, ct);
        }
    }

    // ------------------------------------------------------------------------
    // Core loop: pra cada data candidato dentro do cycle × source, cria uma
    // IncomeEntry se ainda não existir. Idempotente por (UserId, SourceId,
    // ExpectedDate).
    // ------------------------------------------------------------------------
    private static async Task MaterializeEntriesAsync(
        IApplicationDbContext db,
        IncomeSource source,
        Cycle cycle,
        CancellationToken ct)
    {
        // Data mínima efetiva no cycle: max(cycle.StartDate, source.StartDate).
        // Máxima: min(cycle.EndDate, source.EndDate ?? cycle.EndDate).
        var effectiveStart = cycle.StartDate > source.StartDate
            ? cycle.StartDate
            : source.StartDate;

        var effectiveEnd = source.EndDate.HasValue && source.EndDate.Value < cycle.EndDate
            ? source.EndDate.Value
            : cycle.EndDate;

        if (effectiveStart > effectiveEnd)
        {
            return; // sem interseção efetiva
        }

        foreach (var expectedDate in ExpectedDatesInRange(effectiveStart, effectiveEnd, source.ExpectedDay))
        {
            // Idempotência: já existe entry pra essa combinação? Skip.
            var exists = await db.IncomeEntries
                .AnyAsync(
                    e => e.UserId == source.UserId
                      && e.SourceId == source.Id
                      && e.ExpectedDate == expectedDate,
                    ct);

            if (exists)
            {
                continue;
            }

            db.IncomeEntries.Add(new IncomeEntry
            {
                Id = Guid.CreateVersion7(),
                UserId = source.UserId,
                SourceId = source.Id,
                ExpectedAmount = source.DefaultAmount,
                ExpectedDate = expectedDate,
                Status = IncomeStatus.Expected,
            });
        }
    }

    /// <summary>
    /// Enumera datas dentro de [rangeStart, rangeEnd] cujo dia é
    /// <paramref name="expectedDay"/>. Se o mês tem menos dias que
    /// expectedDay (ex: fevereiro + expectedDay=31), cap no último dia
    /// do mês em vez de pular.
    /// </summary>
    private static IEnumerable<DateOnly> ExpectedDatesInRange(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        short expectedDay)
    {
        var year = rangeStart.Year;
        var month = rangeStart.Month;

        while (true)
        {
            // Primeiro dia do mês corrente — se já ultrapassou rangeEnd, para.
            var monthAnchor = new DateOnly(year, month, 1);
            if (monthAnchor > rangeEnd)
            {
                yield break;
            }

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var day = Math.Min((int)expectedDay, daysInMonth);
            var candidate = new DateOnly(year, month, day);

            if (candidate >= rangeStart && candidate <= rangeEnd)
            {
                yield return candidate;
            }

            // Avança 1 mês.
            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }
    }
}
