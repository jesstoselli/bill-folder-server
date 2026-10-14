using BillFolder.Application.UseCases.Cards;
using BillFolder.Application.UseCases.Cycles;
using BillFolder.Application.UseCases.Incomes;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using BillFolder.Infrastructure.Time;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace BillFolder.Api.Tests.Time;

public sealed class BusinessCivilDateTests
{
    [Theory]
    [InlineData("2026-10-08T02:59:59Z", "2026-10-07")]
    [InlineData("2026-10-08T03:00:00Z", "2026-10-08")]
    public void Today_ChangesOnlyAtSaoPauloLocalMidnight(string utcInstant, string expectedToday)
    {
        var provider = CreateProvider(utcInstant);

        Assert.Equal(DateOnly.Parse(expectedToday, CultureInfo.InvariantCulture), provider.Today);
    }

    [Theory]
    [InlineData("2026-10-08T02:59:59Z", ExpenseStatus.Pending, IncomeStatus.Expected, CardStatementStatus.Open, true)]
    [InlineData("2026-10-08T03:00:00Z", ExpenseStatus.Overdue, IncomeStatus.Late, CardStatementStatus.Closed, false)]
    public void CivilToday_DrivesExpenseIncomeCardAndCycleBoundaries(
        string utcInstant,
        ExpenseStatus expectedExpense,
        IncomeStatus expectedIncome,
        CardStatementStatus expectedStatement,
        bool expectedPreviousCycleCurrent)
    {
        var today = CreateProvider(utcInstant).Today;
        var expense = new Expense
        {
            Status = ExpenseStatus.Pending,
            DueDate = new DateOnly(2026, 10, 7),
        };
        var income = new IncomeEntry
        {
            Status = IncomeStatus.Expected,
            ExpectedDate = new DateOnly(2026, 10, 7),
        };
        var previousCycle = new Cycle
        {
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 7),
        };

        Assert.Equal(expectedExpense, expense.ComputeDisplayStatus(today));
        Assert.Equal(expectedIncome, IncomeEntriesService.ComputeDisplayStatus(income, today));
        Assert.Equal(
            expectedStatement,
            CardStatementExtensions.EffectiveStatus(
                CardStatementStatus.Open,
                new DateOnly(2026, 10, 8),
                today));
        Assert.Equal(expectedPreviousCycleCurrent, CyclesService.IsCurrent(previousCycle, today));
    }

    private static BusinessDateProvider CreateProvider(string utcInstant) =>
        new(
            new FixedTimeProvider(DateTimeOffset.Parse(utcInstant, CultureInfo.InvariantCulture)),
            Options.Create(new BusinessTimeOptions { TimeZoneId = "America/Sao_Paulo" }));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
