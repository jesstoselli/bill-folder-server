using BillFolder.Application.Dtos.Home;
using BillFolder.Application.UseCases.Home;
using BillFolder.Domain.Entities;
using FluentAssertions;

namespace BillFolder.Api.Tests.Home;

/// <summary>
/// Testa a agregação pura do breakdown por categoria (sem DB). Foca na regra
/// do bucket sintético "Ajustes": só entram OUTFLOWS de cycle adjustments,
/// somados numa única fatia; inflows nunca viram fatia.
/// </summary>
public sealed class CategoryBreakdownTests
{
    private static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Category MakeCategory() => new()
    {
        Id = CategoryId,
        Key = "food",
        NamePt = "Alimentação",
    };

    private static Expense MakeExpense(decimal amount)
    {
        var category = MakeCategory();
        return new Expense
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Category = category,
            Label = "Mercado",
            ExpectedAmount = amount,
        };
    }

    [Fact]
    public void OutflowAdjustments_ProduceSingleAjustesSlice_WithSummedAmount()
    {
        var expenses = new List<Expense> { MakeExpense(100m) };
        var dailyExpenses = new List<DailyExpense>();
        var cardSlices = new List<HomeStatementCategoryProjection>();

        // Soma de outflows do ciclo = 30 + 20 = 50
        var result = HomeService.BuildCategoryBreakdown(
            expenses, dailyExpenses, cardSlices, adjustmentsOutflows: 50m);

        var ajustes = result.SingleOrDefault(c => c.CategoryName == "Ajustes");
        ajustes.Should().NotBeNull();
        ajustes!.Amount.Should().Be(50m);
        ajustes.CategoryId.Should().Be(Guid.Empty);

        // A categoria real continua presente e o topo é a de maior valor.
        result.Should().HaveCount(2);
        result[0].Amount.Should().Be(100m);
    }

    [Fact]
    public void NoOutflowAdjustments_ProducesNoAjustesSlice()
    {
        var expenses = new List<Expense> { MakeExpense(100m) };
        var dailyExpenses = new List<DailyExpense>();
        var cardSlices = new List<HomeStatementCategoryProjection>();

        var result = HomeService.BuildCategoryBreakdown(
            expenses, dailyExpenses, cardSlices, adjustmentsOutflows: 0m);

        result.Should().NotContain(c => c.CategoryName == "Ajustes");
        result.Should().HaveCount(1);
    }
}
