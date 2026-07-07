using BillFolder.Application.UseCases.Home;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Home;

/// <summary>
/// A matemática mais delicada da feature: uma despesa provisionada parcialmente
/// paga deve reservar só o que FALTA (ExpectedAmount − PaidToDate) e contar o
/// já-pago como realizado — de modo que Reserved + Realized = ExpectedAmount,
/// sem contar em dobro. Protege o "nunca gastar além do mês cheio".
/// </summary>
public class ExpenseBucketsTests
{
    private static Expense Normal(decimal expected, ExpenseStatus status, decimal? actual = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExpectedAmount = expected,
            ActualAmount = actual,
            Status = status,
        };

    private static Expense Provisioned(
        decimal expected, int total, int paidCount, decimal paidToDate,
        ExpenseStatus status = ExpenseStatus.Pending, decimal? actual = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExpectedAmount = expected,
            ActualAmount = actual,
            Status = status,
            OccurrenceAmount = expected / total,
            OccurrencesTotal = total,
            OccurrencesPaid = paidCount,
            PaidToDate = paidToDate,
        };

    [Fact]
    public void Normal_pending_reserves_full_expected()
    {
        var (reserved, realized) = HomeService.ComputeExpenseBuckets(new[] { Normal(400m, ExpenseStatus.Pending) });
        Assert.Equal(400m, reserved);
        Assert.Equal(0m, realized);
    }

    [Fact]
    public void Normal_paid_uses_actual_as_realized_and_reserves_nothing()
    {
        var (reserved, realized) = HomeService.ComputeExpenseBuckets(
            new[] { Normal(400m, ExpenseStatus.Paid, actual: 380m) });
        Assert.Equal(0m, reserved);
        Assert.Equal(380m, realized);
    }

    [Fact]
    public void Provisioned_in_progress_splits_expected_into_reserved_plus_realized()
    {
        // Terapia R$600/mês (4 sessões de 150), 2 pagas → paidToDate 300.
        var (reserved, realized) = HomeService.ComputeExpenseBuckets(
            new[] { Provisioned(expected: 600m, total: 4, paidCount: 2, paidToDate: 300m) });

        Assert.Equal(300m, reserved);              // falta pagar
        Assert.Equal(300m, realized);              // já pago
        Assert.Equal(600m, reserved + realized);   // mês cheio, sem dobro
    }

    [Fact]
    public void Provisioned_untouched_reserves_full_month()
    {
        var (reserved, realized) = HomeService.ComputeExpenseBuckets(
            new[] { Provisioned(expected: 750m, total: 5, paidCount: 0, paidToDate: 0m) });
        Assert.Equal(750m, reserved);
        Assert.Equal(0m, realized);
    }

    [Fact]
    public void Provisioned_fully_paid_is_all_realized()
    {
        var (reserved, realized) = HomeService.ComputeExpenseBuckets(
            new[] { Provisioned(expected: 600m, total: 4, paidCount: 4, paidToDate: 600m,
                                status: ExpenseStatus.Paid, actual: 600m) });
        Assert.Equal(0m, reserved);
        Assert.Equal(600m, realized);
    }

    [Fact]
    public void Mixed_set_totals_correctly()
    {
        var expenses = new[]
        {
            Normal(400m, ExpenseStatus.Pending),                                   // reserved 400
            Normal(200m, ExpenseStatus.Paid, actual: 200m),                        // realized 200
            Provisioned(600m, 4, 1, 150m),                                         // reserved 450, realized 150
        };

        var (reserved, realized) = HomeService.ComputeExpenseBuckets(expenses);

        Assert.Equal(400m + 450m, reserved);
        Assert.Equal(200m + 150m, realized);
    }
}
