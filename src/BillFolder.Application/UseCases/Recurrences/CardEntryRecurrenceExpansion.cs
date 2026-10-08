using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.UseCases.Cards;
using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Recurrences;

/// <summary>
/// Materializa CardEntryRecurrences (assinaturas mensais fixas — Netflix,
/// Spotify) em UMA compra à vista (installmentsCount = 1) por ciclo, colocada
/// na fatura certa via CardCycleCalculator. Espelha o ExpenseRecurrenceExpansion,
/// com a diferença de que a cobrança é MENSAL num dia fixo do mês (DayOfMonth),
/// não semanal.
///
/// Chamada em 2 pontos (igual à despesa provisionada):
///   - CardEntryRecurrencesService.CreateAsync → ExpandForTemplateAsync
///   - CyclesService.CreateAsync / GenerateForwardCyclesAsync → ExpandForCycleAsync
///
/// Idempotente por (UserId, TemplateId, PurchaseDate). Só ADICIONA ao change
/// tracker; SaveChangesAsync é do caller.
/// </summary>
public static class CardEntryRecurrenceExpansion
{
    public static async Task ExpandForTemplateAsync(
        IApplicationDbContext db,
        CardEntryRecurrence recurrence,
        DateOnly today,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recurrence);
        if (!recurrence.IsActive)
        {
            return;
        }

        var end = recurrence.EndDate ?? today.AddYears(5);

        var cycles = await db.Cycles
            .Where(c => c.UserId == recurrence.UserId
                     && c.EndDate >= recurrence.StartDate
                     && c.StartDate <= end)
            .ToListAsync(ct);

        foreach (var cycle in cycles)
        {
            await MaterializeAsync(db, recurrence, cycle, ct);
        }
    }

    public static async Task ExpandForCycleAsync(
        IApplicationDbContext db,
        Cycle cycle,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        var recurrences = await db.CardEntryRecurrences
            .Where(r => r.UserId == cycle.UserId
                     && r.IsActive
                     && r.StartDate <= cycle.EndDate
                     && (r.EndDate == null || r.EndDate >= cycle.StartDate))
            .ToListAsync(ct);

        foreach (var recurrence in recurrences)
        {
            await MaterializeAsync(db, recurrence, cycle, ct);
        }
    }

    private static async Task MaterializeAsync(
        IApplicationDbContext db,
        CardEntryRecurrence recurrence,
        Cycle cycle,
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

        var purchaseDate = RecurrenceDates.MonthlyDateInRange(effectiveStart, effectiveEnd, recurrence.DayOfMonth);
        if (purchaseDate is not { } date)
        {
            return; // a cobrança mensal não cai nesse range
        }

        // Idempotência: uma cobrança por (user, template, purchaseDate).
        var exists = await db.CardEntries
            .AnyAsync(
                e => e.UserId == recurrence.UserId
                  && e.TemplateId == recurrence.Id
                  && e.PurchaseDate == date,
                ct);
        if (exists)
        {
            return;
        }

        // Precisa do cartão (closing_day/due_day) pra posicionar a fatura.
        var card = await db.CreditCardAccounts
            .FirstOrDefaultAsync(c => c.Id == recurrence.CardId && c.UserId == recurrence.UserId, ct);
        if (card is null)
        {
            return; // cartão sumiu — ignora defensivamente
        }

        await CardEntriesService.MaterializeChargeAsync(
            db,
            recurrence.UserId,
            card,
            date,
            recurrence.DefaultLabel,
            recurrence.DefaultAmount,
            installmentsCount: 1,
            recurrence.DefaultCategoryId,
            templateId: recurrence.Id,
            notes: null,
            ct);
    }
}
