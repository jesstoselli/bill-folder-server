using BillFolder.Application.UseCases.Home;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Home;

/// <summary>
/// O "esperado no mês" deve refletir o valor EFETIVO de cada entry: uma
/// entry já recebida vale o ActualAmount (o que realmente entrou), não o
/// ExpectedAmount original. Sem isso, uma renda reduzida no mês (ex: férias)
/// continuava contando cheia no total esperado e no `remaining`, inflando o
/// "quanto sobra" e levando a saques indevidos da poupança.
/// </summary>
public class IncomeExpectedTests
{
    private static IncomeEntry Entry(
        decimal expected, IncomeStatus status, decimal? actual = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExpectedAmount = expected,
            ActualAmount = actual,
            Status = status,
        };

    [Fact]
    public void Expected_entry_counts_expected_amount()
    {
        var total = HomeService.ComputeExpectedIncome(
            new[] { Entry(2000m, IncomeStatus.Expected) });
        Assert.Equal(2000m, total);
    }

    [Fact]
    public void Received_entry_counts_actual_not_expected()
    {
        // Stone: esperado 7679.10, recebido 3979.10 (mês de férias).
        var total = HomeService.ComputeExpectedIncome(
            new[] { Entry(7679.10m, IncomeStatus.Received, actual: 3979.10m) });
        Assert.Equal(3979.10m, total);
    }

    [Fact]
    public void Received_without_actual_falls_back_to_expected()
    {
        var total = HomeService.ComputeExpectedIncome(
            new[] { Entry(2000m, IncomeStatus.Received, actual: null) });
        Assert.Equal(2000m, total);
    }

    [Fact]
    public void NotOccurred_entry_is_excluded()
    {
        var total = HomeService.ComputeExpectedIncome(
            new[] { Entry(2000m, IncomeStatus.NotOccurred) });
        Assert.Equal(0m, total);
    }

    [Fact]
    public void Mixed_cycle_totals_effective_amounts()
    {
        // Reproduz o cenário do bug: 2000 (recebido) + 3979.10 (recebido,
        // reduzido) + 2406.08 (esperado) = 8385.18 — e NÃO 12085.18.
        var entries = new[]
        {
            Entry(2000m, IncomeStatus.Received, actual: 2000m),
            Entry(7679.10m, IncomeStatus.Received, actual: 3979.10m),
            Entry(2406.08m, IncomeStatus.Expected),
        };

        Assert.Equal(8385.18m, HomeService.ComputeExpectedIncome(entries));
    }
}
