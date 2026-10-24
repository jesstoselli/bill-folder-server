using BillFolder.Application.UseCases.Expenses;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Expenses;

/// <summary>
/// Mudar quantas sessões uma despesa provisionada tem no ciclo (férias da
/// terapeuta, sessão extra) recalcula o total = valor por sessão × sessões, e
/// a situação acompanha: quitada quando as pagas alcançam o novo total,
/// pendente de novo quando o total passa das pagas.
/// </summary>
public class UpdateOccurrencesTests
{
    private static Expense Provisioned(int total, int paid, ExpenseStatus status = ExpenseStatus.Pending) =>
        new()
        {
            Id = Guid.NewGuid(),
            OccurrenceAmount = 200m,
            OccurrencesTotal = total,
            ExpectedAmount = 200m * total,
            OccurrencesPaid = paid,
            PaidToDate = 200m * paid,
            PaidDate = paid > 0 ? new DateOnly(2026, 12, 8) : null,
            Status = status,
            ActualAmount = status == ExpenseStatus.Paid ? 200m * paid : null,
        };

    [Fact]
    public void Fewer_sessions_recompute_the_total_and_keep_what_was_paid()
    {
        var expense = Provisioned(total: 5, paid: 1);

        ExpensesService.ApplyOccurrencesTotal(expense, 3);

        Assert.Equal(3, expense.OccurrencesTotal);
        Assert.Equal(600m, expense.ExpectedAmount);
        Assert.Equal(1, expense.OccurrencesPaid);
        Assert.Equal(200m, expense.PaidToDate);
        Assert.Equal(ExpenseStatus.Pending, expense.Status);
    }

    [Fact]
    public void Reaching_the_paid_sessions_settles_the_expense()
    {
        var expense = Provisioned(total: 5, paid: 3);

        ExpensesService.ApplyOccurrencesTotal(expense, 3);

        Assert.Equal(ExpenseStatus.Paid, expense.Status);
        Assert.Equal(600m, expense.ActualAmount);
    }

    [Fact]
    public void An_extra_session_reopens_a_settled_expense()
    {
        var expense = Provisioned(total: 4, paid: 4, status: ExpenseStatus.Paid);

        ExpensesService.ApplyOccurrencesTotal(expense, 5);

        Assert.Equal(1000m, expense.ExpectedAmount);
        Assert.Equal(ExpenseStatus.Pending, expense.Status);
        Assert.Null(expense.ActualAmount);
    }
}
