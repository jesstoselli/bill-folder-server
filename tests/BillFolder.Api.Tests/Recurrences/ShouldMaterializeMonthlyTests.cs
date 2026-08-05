using BillFolder.Application.UseCases.Recurrences;

namespace BillFolder.Api.Tests.Recurrences;

/// <summary>
/// Recorrência mensal de despesa é não-retroativa na criação: só materializa o
/// ciclo atual se o vencimento não passou. No rollover (notBefore null) sempre
/// gera. Predicado puro e determinístico.
/// </summary>
public class ShouldMaterializeMonthlyTests
{
    private static readonly DateOnly Due = new(2026, 8, 5);

    [Fact]
    public void Null_notBefore_always_materializes()
        => Assert.True(ExpenseRecurrenceExpansion.ShouldMaterializeMonthly(Due, null));

    [Fact]
    public void Due_before_notBefore_does_not_materialize()
        => Assert.False(ExpenseRecurrenceExpansion.ShouldMaterializeMonthly(Due, new DateOnly(2026, 8, 20)));

    [Fact]
    public void Due_on_or_after_notBefore_materializes()
    {
        Assert.True(ExpenseRecurrenceExpansion.ShouldMaterializeMonthly(Due, new DateOnly(2026, 8, 5)));
        Assert.True(ExpenseRecurrenceExpansion.ShouldMaterializeMonthly(Due, new DateOnly(2026, 8, 1)));
    }
}
