using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Recurrences;

/// <summary>
/// Materializa ExpenseRecurrences do tipo Weekly em UMA despesa provisionada
/// por ciclo. Espelha o IncomeSourceExpansion, com duas diferenças:
///   - Gera 1 Expense por ciclo (não 1 por data). O valor cheio já reserva o
///     mês inteiro; a quitação é por ocorrência (semana) via PayOccurrence.
///   - OccurrencesTotal = nº de vezes que o Weekday cai no ciclo (4 ou 5);
///     ExpectedAmount = DefaultAmount × OccurrencesTotal; DueDate = 1ª ocorrência.
///
/// Chamada em 2 pontos (igual ao income):
///   - ExpenseRecurrencesService.CreateAsync → ExpandForTemplateAsync
///   - CyclesService.CreateAsync / GenerateForwardCyclesAsync → ExpandForCycleAsync
///
/// Idempotente por (UserId, TemplateId, DueDate). Só ADICIONA ao change tracker;
/// SaveChangesAsync é do caller.
/// </summary>
public static class ExpenseRecurrenceExpansion
{
    public static async Task ExpandForTemplateAsync(
        IApplicationDbContext db,
        ExpenseRecurrence recurrence,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recurrence);
        if (!recurrence.IsActive)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var end = recurrence.EndDate ?? today.AddYears(5);

        var cycles = await db.Cycles
            .Where(c => c.UserId == recurrence.UserId
                     && c.EndDate >= recurrence.StartDate
                     && c.StartDate <= end)
            .ToListAsync(ct);

        // notBefore = hoje: na criação, Monthly não gera ocorrência já vencida
        // (não-retroativa). Weekly ignora notBefore.
        foreach (var cycle in cycles)
        {
            await MaterializeAsync(db, recurrence, cycle, today, ct);
        }
    }

    public static async Task ExpandForCycleAsync(
        IApplicationDbContext db,
        Cycle cycle,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        var recurrences = await db.ExpenseRecurrences
            .Where(r => r.UserId == cycle.UserId
                     && r.IsActive
                     && r.StartDate <= cycle.EndDate
                     && (r.EndDate == null || r.EndDate >= cycle.StartDate))
            .ToListAsync(ct);

        // Rollover: notBefore null → gera o ciclo novo mesmo se já venceu (a
        // recorrência é anterior ao ciclo, não é retroativo).
        foreach (var recurrence in recurrences)
        {
            await MaterializeAsync(db, recurrence, cycle, notBefore: null, ct);
        }
    }

    /// <summary>Monthly não-retroativa: null sempre gera; caso contrário só se
    /// o vencimento não passou (dueDate &gt;= notBefore). Puro/determinístico.</summary>
    internal static bool ShouldMaterializeMonthly(DateOnly dueDate, DateOnly? notBefore)
        => notBefore is null || dueDate >= notBefore;

    private static async Task MaterializeAsync(
        IApplicationDbContext db,
        ExpenseRecurrence recurrence,
        Cycle cycle,
        DateOnly? notBefore,
        CancellationToken ct)
    {
        var effectiveStart = cycle.StartDate > recurrence.StartDate ? cycle.StartDate : recurrence.StartDate;
        var effectiveEnd = recurrence.EndDate.HasValue && recurrence.EndDate.Value < cycle.EndDate
            ? recurrence.EndDate.Value
            : cycle.EndDate;
        if (effectiveStart > effectiveEnd)
        {
            return;
        }

        if (recurrence.Frequency == ExpenseRecurrenceFrequency.Monthly)
        {
            await MaterializeMonthlyAsync(db, recurrence, effectiveStart, effectiveEnd, notBefore, ct);
        }
        else
        {
            await MaterializeWeeklyAsync(db, recurrence, effectiveStart, effectiveEnd, ct);
        }
    }

    // Weekly → despesa provisionada (reserva o mês, baixa por ocorrência).
    private static async Task MaterializeWeeklyAsync(
        IApplicationDbContext db,
        ExpenseRecurrence recurrence,
        DateOnly effectiveStart,
        DateOnly effectiveEnd,
        CancellationToken ct)
    {
        // Weekly sem weekday é dado inválido — ignora defensivamente.
        if (recurrence.Weekday is not { } weekday)
        {
            return;
        }

        var dates = WeekdayDatesInRange(effectiveStart, effectiveEnd, weekday);
        if (dates.Count == 0)
        {
            return; // nenhuma ocorrência desse weekday no ciclo
        }

        var dueDate = dates[0];
        if (await ExistsAsync(db, recurrence, dueDate, ct))
        {
            return;
        }

        db.Expenses.Add(new Expense
        {
            Id = Guid.CreateVersion7(),
            UserId = recurrence.UserId,
            TemplateId = recurrence.Id,
            DueDate = dueDate,
            Label = recurrence.DefaultLabel,
            ExpectedAmount = recurrence.DefaultAmount * dates.Count,
            CategoryId = recurrence.DefaultCategoryId,
            Status = ExpenseStatus.Pending,
            OccurrenceAmount = recurrence.DefaultAmount,
            OccurrencesTotal = dates.Count,
            OccurrencesPaid = 0,
            PaidToDate = 0m,
        });
    }

    // Monthly → despesa comum, 1 por ciclo, no DueDay (não-retroativa na criação).
    private static async Task MaterializeMonthlyAsync(
        IApplicationDbContext db,
        ExpenseRecurrence recurrence,
        DateOnly effectiveStart,
        DateOnly effectiveEnd,
        DateOnly? notBefore,
        CancellationToken ct)
    {
        if (RecurrenceDates.MonthlyDateInRange(effectiveStart, effectiveEnd, recurrence.DueDay ?? 1) is not { } dueDate)
        {
            return; // o dia do mês não cai nesse ciclo
        }

        if (!ShouldMaterializeMonthly(dueDate, notBefore))
        {
            return;
        }

        if (await ExistsAsync(db, recurrence, dueDate, ct))
        {
            return;
        }

        db.Expenses.Add(new Expense
        {
            Id = Guid.CreateVersion7(),
            UserId = recurrence.UserId,
            TemplateId = recurrence.Id,
            DueDate = dueDate,
            Label = recurrence.DefaultLabel,
            ExpectedAmount = recurrence.DefaultAmount,
            CategoryId = recurrence.DefaultCategoryId,
            Status = ExpenseStatus.Pending,
        });
    }

    // Idempotência: uma despesa por (user, template, dueDate).
    private static Task<bool> ExistsAsync(
        IApplicationDbContext db, ExpenseRecurrence recurrence, DateOnly dueDate, CancellationToken ct)
        => db.Expenses.AnyAsync(
            e => e.UserId == recurrence.UserId && e.TemplateId == recurrence.Id && e.DueDate == dueDate,
            ct);

    /// <summary>
    /// Todas as datas em [start, end] cujo dia-da-semana é <paramref name="weekday"/>
    /// (0=domingo … 6=sábado, casa com DayOfWeek), em ordem ascendente.
    /// </summary>
    internal static IReadOnlyList<DateOnly> WeekdayDatesInRange(DateOnly start, DateOnly end, int weekday)
    {
        var result = new List<DateOnly>();
        if (start > end)
        {
            return result;
        }

        // Avança de start até a 1ª data com o weekday desejado.
        var offset = (weekday - (int)start.DayOfWeek + 7) % 7;
        for (var d = start.AddDays(offset); d <= end; d = d.AddDays(7))
        {
            result.Add(d);
        }

        return result;
    }
}
