using BillFolder.Application.UseCases.Savings;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Savings;

/// <summary>
/// Saldo corrente da poupança = InitialBalance + Σ transações com sinal.
/// Depósito/rendimento/transferência-entrada somam; saque/transferência-saída
/// subtraem. É o número que a tela de poupança passa a exibir.
/// </summary>
public class SavingsBalanceTests
{
    private static SavingsTransaction Tx(SavingsTransactionType type, decimal amount) =>
        new() { Type = type, Amount = amount };

    [Fact]
    public void No_transactions_returns_initial_balance()
    {
        Assert.Equal(300m, SavingsBalance.Compute(300m, System.Array.Empty<SavingsTransaction>()));
    }

    [Fact]
    public void Deposit_yield_and_transfer_in_add()
    {
        var balance = SavingsBalance.Compute(100m, new[]
        {
            Tx(SavingsTransactionType.Deposit, 50m),
            Tx(SavingsTransactionType.Yield, 10m),
            Tx(SavingsTransactionType.TransferIn, 5m),
        });
        Assert.Equal(165m, balance);
    }

    [Fact]
    public void Withdrawal_and_transfer_out_subtract()
    {
        var balance = SavingsBalance.Compute(100m, new[]
        {
            Tx(SavingsTransactionType.Withdrawal, 30m),
            Tx(SavingsTransactionType.TransferOut, 20m),
        });
        Assert.Equal(50m, balance);
    }

    [Fact]
    public void Mixed_types_net_correctly()
    {
        // 200 + 100(dep) + 10(yield) + 15(in) - 50(saque) - 25(out) = 250
        var balance = SavingsBalance.Compute(200m, new[]
        {
            Tx(SavingsTransactionType.Deposit, 100m),
            Tx(SavingsTransactionType.Yield, 10m),
            Tx(SavingsTransactionType.TransferIn, 15m),
            Tx(SavingsTransactionType.Withdrawal, 50m),
            Tx(SavingsTransactionType.TransferOut, 25m),
        });
        Assert.Equal(250m, balance);
    }
}
