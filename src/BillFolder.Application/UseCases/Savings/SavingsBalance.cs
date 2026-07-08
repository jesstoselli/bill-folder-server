using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Application.UseCases.Savings;

/// <summary>
/// Saldo corrente de uma poupança: InitialBalance + soma das transações com
/// sinal por tipo. Fonte única da regra de sinais (usada pelo
/// SavingsAccountsService pra popular CurrentBalance).
/// </summary>
public static class SavingsBalance
{
    /// <summary>+1 pra entradas (depósito/rendimento/transferência-entrada),
    /// −1 pra saídas (saque/transferência-saída).</summary>
    internal static decimal Signed(SavingsTransactionType type, decimal amount) => type switch
    {
        SavingsTransactionType.Deposit => amount,
        SavingsTransactionType.Yield => amount,
        SavingsTransactionType.TransferIn => amount,
        SavingsTransactionType.Withdrawal => -amount,
        SavingsTransactionType.TransferOut => -amount,
        _ => 0m,
    };

    public static decimal Compute(decimal initialBalance, IEnumerable<SavingsTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        var delta = 0m;
        foreach (var t in transactions)
        {
            delta += Signed(t.Type, t.Amount);
        }
        return initialBalance + delta;
    }
}
