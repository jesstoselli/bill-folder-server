using BillFolder.Application.UseCases.Expenses;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Expenses;

/// <summary>
/// Ao excluir uma despesa recorrente, o escopo decide o conjunto atingido:
/// só a ocorrência-alvo, ou ela + as seguintes. Ocorrências passadas já quitadas
/// (Paid) nunca são apagadas — o histórico financeiro permanece intacto.
/// Helper puro (sem DB), testado como ComputeExpenseBuckets.
/// </summary>
public class RecurrenceScopeDeleteTests
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
    public void This_returns_only_the_target()
    {
        var jun = Occurrence(new DateOnly(2026, 6, 1));
        var jul = Occurrence(new DateOnly(2026, 7, 1));
        var ago = Occurrence(new DateOnly(2026, 8, 1));

        var ids = ExpensesService.OccurrencesToDelete(
            new[] { jun, jul, ago }, target: jul, scope: RecurrenceScope.This);

        Assert.Equal(new[] { jul.Id }, ids);
    }

    [Fact]
    public void ThisAndFollowing_returns_target_plus_later_occurrences()
    {
        var jun = Occurrence(new DateOnly(2026, 6, 1));
        var jul = Occurrence(new DateOnly(2026, 7, 1));
        var ago = Occurrence(new DateOnly(2026, 8, 1));

        var ids = ExpensesService.OccurrencesToDelete(
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

        var ids = ExpensesService.OccurrencesToDelete(
            new[] { jul, agoPaid, set }, target: jul, scope: RecurrenceScope.ThisAndFollowing);

        Assert.Equal(new HashSet<Guid> { jul.Id, set.Id }, ids.ToHashSet());
        Assert.DoesNotContain(agoPaid.Id, ids);
    }
}
