using BillFolder.Domain.Enums;

namespace BillFolder.Domain.Entities;

/// <summary>
/// Extensões de domínio sobre Expense. Centraliza regras computadas que
/// podem ser consumidas por qualquer service sem duplicação.
/// </summary>
public static class ExpenseExtensions
{
    /// <summary>
    /// Status de exibição da despesa, considerando a regra de overdue
    /// computado: uma despesa Pending cujo vencimento já passou é mostrada
    /// como Overdue, mesmo que o valor stored seja Pending.
    ///
    /// O backend nunca persiste Overdue como status — sempre Pending no
    /// banco e o display deriva da data atual. Isso evita a necessidade
    /// de um cron diário que mude Pending → Overdue ao virar o dia.
    /// </summary>
    public static ExpenseStatus ComputeDisplayStatus(this Expense expense, DateOnly today) =>
        expense.Status == ExpenseStatus.Pending && expense.DueDate < today
            ? ExpenseStatus.Overdue
            : expense.Status;
}
