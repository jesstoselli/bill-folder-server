using BillFolder.Application.UseCases.Recurrences;

namespace BillFolder.Api.Tests.Recurrences;

/// <summary>
/// A data em que uma assinatura mensal (Netflix, Spotify) cai dentro do range
/// de um ciclo. É isso que define a PurchaseDate do CardEntry auto-gerado.
/// Dia é clampado ao último dia do mês; se o dia não cai no range parcial,
/// retorna null (nenhuma cobrança nesse ciclo).
/// </summary>
public class MonthlyDateInCycleTests
{
    [Fact]
    public void Returns_the_day_within_a_full_calendar_month()
    {
        var result = CardEntryRecurrenceExpansion.MonthlyDateInRange(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), 15);

        Assert.Equal(new DateOnly(2026, 7, 15), result);
    }

    [Fact]
    public void Clamps_day_to_last_day_of_a_short_month()
    {
        var result = CardEntryRecurrenceExpansion.MonthlyDateInRange(
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), 31);

        Assert.Equal(new DateOnly(2026, 2, 28), result);
    }

    [Fact]
    public void Returns_null_when_day_is_outside_a_partial_range()
    {
        var result = CardEntryRecurrenceExpansion.MonthlyDateInRange(
            new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 31), 5);

        Assert.Null(result);
    }
}
