using BillFolder.Application.UseCases.Cards;
using BillFolder.Domain.Enums;
using System.Globalization;

namespace BillFolder.Api.Tests.Cards;

public class CardStatementLifecycleTests
{
    [Theory]
    [InlineData("2026-07-16", CardStatementStatus.Open)]
    [InlineData("2026-07-17", CardStatementStatus.Closed)]
    [InlineData("2026-07-18", CardStatementStatus.Closed)]
    public void EffectiveStatus_derives_open_or_closed_from_period_end(
        string todayIso, CardStatementStatus expected)
    {
        var status = CardStatementExtensions.EffectiveStatus(
            CardStatementStatus.Open,
            new DateOnly(2026, 7, 17),
            DateOnly.Parse(todayIso, CultureInfo.InvariantCulture));

        Assert.Equal(expected, status);
    }

    [Fact]
    public void EffectiveStatus_keeps_paid()
    {
        var status = CardStatementExtensions.EffectiveStatus(
            CardStatementStatus.Paid,
            new DateOnly(2026, 8, 17),
            new DateOnly(2026, 7, 1));

        Assert.Equal(CardStatementStatus.Paid, status);
    }

    [Fact]
    public void MatchesEffectiveStatus_treats_persisted_open_as_closed_on_period_end()
    {
        var matches = CardStatementExtensions.MatchesEffectiveStatus(
            CardStatementStatus.Open,
            new DateOnly(2026, 7, 17),
            new DateOnly(2026, 7, 17),
            CardStatementStatus.Closed);

        Assert.True(matches);
    }

    [Fact]
    public void Purchase_on_closing_day_goes_to_next_statement()
    {
        var result = CardCycleCalculator.ComputeStatementForPurchase(
            new DateOnly(2026, 6, 17), closingDay: 17, dueDay: 25);

        Assert.Equal(new DateOnly(2026, 7, 17), result.PeriodEnd);
        Assert.Equal(new DateOnly(2026, 7, 25), result.DueDate);
    }

    [Fact]
    public void Purchase_on_clamped_closing_day_goes_to_next_statement()
    {
        var result = CardCycleCalculator.ComputeStatementForPurchase(
            new DateOnly(2026, 2, 28), closingDay: 31, dueDay: 10);

        Assert.Equal(new DateOnly(2026, 3, 31), result.PeriodEnd);
    }
}
