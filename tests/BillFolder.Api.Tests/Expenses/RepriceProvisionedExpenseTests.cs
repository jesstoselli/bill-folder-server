using BillFolder.Application.UseCases.Expenses;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Expenses;

/// <summary>
/// Ao reajustar o valor POR SESSÃO de uma despesa provisionada, o total do ciclo
/// (ExpectedAmount) recalcula = valor × OccurrencesTotal dela, e PaidToDate/
/// OccurrencesPaid ficam (a reserva se ajusta sozinha). O escopo decide quais
/// ocorrências recebem o novo valor: só a alvo, ou ela + as seguintes não-pagas.
/// Helpers puros (sem DB): mesma forma do OccurrencesToDelete.
/// </summary>
public class RepriceProvisionedExpenseTests
{
    private static readonly Guid TemplateId = Guid.NewGuid();

    private static Expense Occurrence(DateOnly dueDate, ExpenseStatus status = ExpenseStatus.Pending) =>
        new()
        {
            Id = Guid.NewGuid(),
            TemplateId = TemplateId,
            DueDate = dueDate,
            Status = status,
        };

    [Fact]
    public void RepriceOccurrence_recomputes_expected_amount_and_preserves_paid()
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OccurrencesTotal = 4,
            OccurrenceAmount = 150m,
            ExpectedAmount = 600m,
            PaidToDate = 300m,
            OccurrencesPaid = 2,
        };

        ExpensesService.RepriceOccurrence(expense, 170m);

        Assert.Equal(170m, expense.OccurrenceAmount);
        Assert.Equal(680m, expense.ExpectedAmount);
        Assert.Equal(300m, expense.PaidToDate);
        Assert.Equal(2, expense.OccurrencesPaid);
    }

    [Fact]
    public void RepriceOccurrence_uses_own_total_for_five_week_cycle()
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OccurrencesTotal = 5,
            OccurrenceAmount = 150m,
            ExpectedAmount = 750m,
        };

        ExpensesService.RepriceOccurrence(expense, 170m);

        Assert.Equal(850m, expense.ExpectedAmount);
    }

    [Fact]
    public void This_returns_only_the_target()
    {
        var jun = Occurrence(new DateOnly(2026, 6, 1));
        var jul = Occurrence(new DateOnly(2026, 7, 1));
        var ago = Occurrence(new DateOnly(2026, 8, 1));

        var ids = ExpensesService.OccurrencesToReprice(
            new[] { jun, jul, ago }, target: jul, scope: RecurrenceScope.This);

        Assert.Equal(new[] { jul.Id }, ids);
    }

    [Fact]
    public void ThisAndFollowing_returns_target_plus_later_occurrences()
    {
        var jun = Occurrence(new DateOnly(2026, 6, 1));
        var jul = Occurrence(new DateOnly(2026, 7, 1));
        var ago = Occurrence(new DateOnly(2026, 8, 1));

        var ids = ExpensesService.OccurrencesToReprice(
            new[] { jun, jul, ago }, target: jul, scope: RecurrenceScope.ThisAndFollowing);

        Assert.Equal(new HashSet<Guid> { jul.Id, ago.Id }, ids.ToHashSet());
        Assert.DoesNotContain(jun.Id, ids);
    }

    [Fact]
    public void ThisAndFollowing_excludes_paid_future_occurrence()
    {
        var jul = Occurrence(new DateOnly(2026, 7, 1));
        var agoPaid = Occurrence(new DateOnly(2026, 8, 1), ExpenseStatus.Paid);
        var set = Occurrence(new DateOnly(2026, 9, 1));

        var ids = ExpensesService.OccurrencesToReprice(
            new[] { jul, agoPaid, set }, target: jul, scope: RecurrenceScope.ThisAndFollowing);

        Assert.Equal(new HashSet<Guid> { jul.Id, set.Id }, ids.ToHashSet());
        Assert.DoesNotContain(agoPaid.Id, ids);
    }
}
