using BillFolder.Application.UseCases.Home;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Home;

public class CardStatementBucketsTests
{
    [Fact]
    public void Unpaid_statement_is_reserved_by_installment_total()
    {
        var result = HomeService.ComputeCardStatementBuckets(new[]
        {
            new HomeStatementFinancialProjection(CardStatementStatus.Closed, 800m, null),
        });

        Assert.Equal(800m, result.Expected);
        Assert.Equal(0m, result.Paid);
        Assert.Equal(800m, result.TotalCommitted);
    }

    [Fact]
    public void Paid_statement_uses_actual_amount()
    {
        var result = HomeService.ComputeCardStatementBuckets(new[]
        {
            new HomeStatementFinancialProjection(CardStatementStatus.Paid, 800m, 790m),
        });

        Assert.Equal(0m, result.Expected);
        Assert.Equal(790m, result.Paid);
        Assert.Equal(790m, result.TotalCommitted);
    }
}
