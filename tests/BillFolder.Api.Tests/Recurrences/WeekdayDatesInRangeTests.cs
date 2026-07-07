using BillFolder.Application.UseCases.Recurrences;

namespace BillFolder.Api.Tests.Recurrences;

/// <summary>
/// Contagem de ocorrências de um dia-da-semana dentro do range de um ciclo —
/// é isso que define OccurrencesTotal (4 ou 5) e ExpectedAmount de uma despesa
/// provisionada semanal. Asserções determinísticas que não dependem de saber
/// o dia-da-semana de uma data específica.
/// </summary>
public class WeekdayDatesInRangeTests
{
    [Fact]
    public void February_28_days_has_exactly_four_of_every_weekday()
    {
        var start = new DateOnly(2026, 2, 1);
        var end = new DateOnly(2026, 2, 28); // 2026 não é bissexto → 28 dias = 4×7

        for (var wd = 0; wd < 7; wd++)
        {
            var dates = ProvisionedExpenseExpansion.WeekdayDatesInRange(start, end, wd);
            Assert.Equal(4, dates.Count);
        }
    }

    [Fact]
    public void ThirtyOne_day_month_totals_31_with_three_weekdays_occurring_five_times()
    {
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 7, 31); // 31 = 4×7 + 3 → 3 weekdays com 5

        var counts = new List<int>();
        for (var wd = 0; wd < 7; wd++)
        {
            counts.Add(ProvisionedExpenseExpansion.WeekdayDatesInRange(start, end, wd).Count);
        }

        Assert.Equal(31, counts.Sum());
        Assert.Equal(3, counts.Count(c => c == 5));
        Assert.Equal(4, counts.Count(c => c == 4));
    }

    [Fact]
    public void Seven_day_window_has_exactly_one_of_each_weekday()
    {
        var start = new DateOnly(2026, 6, 10);
        var end = new DateOnly(2026, 6, 16);

        for (var wd = 0; wd < 7; wd++)
        {
            Assert.Single(ProvisionedExpenseExpansion.WeekdayDatesInRange(start, end, wd));
        }
    }

    [Fact]
    public void Returns_dates_matching_the_weekday_in_ascending_order()
    {
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 7, 31);
        // Usa o próprio dia-da-semana do dia 1 como alvo → primeira ocorrência é o dia 1.
        var targetWeekday = (int)start.DayOfWeek;

        var dates = ProvisionedExpenseExpansion.WeekdayDatesInRange(start, end, targetWeekday);

        Assert.Equal(start, dates[0]);
        Assert.All(dates, d => Assert.Equal(targetWeekday, (int)d.DayOfWeek));
        var sorted = dates.OrderBy(d => d).ToList();
        Assert.Equal(sorted, dates);
    }
}
