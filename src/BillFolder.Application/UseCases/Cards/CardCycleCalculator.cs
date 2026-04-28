namespace BillFolder.Application.UseCases.Cards;

/// <summary>
/// Lógica pura de cálculo de períodos de fatura. Sem dependências externas
/// — testável de forma isolada.
/// </summary>
public static class CardCycleCalculator
{
    /// <summary>
    /// Dada a data de uma compra e os dias de fechamento/vencimento do cartão,
    /// retorna o período da fatura que vai conter essa compra (parcela 1).
    /// </summary>
    public static (DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate) ComputeStatementForPurchase(
        DateOnly purchaseDate,
        short closingDay,
        short dueDay)
    {
        // Se compra cai antes/no dia de fechamento, entra na fatura desse mês.
        // Senão, entra na fatura do mês seguinte.
        var (closeYear, closeMonth) = purchaseDate.Day <= closingDay
            ? (purchaseDate.Year, purchaseDate.Month)
            : AddMonths(purchaseDate.Year, purchaseDate.Month, 1);

        var periodEnd = ClampDay(closeYear, closeMonth, closingDay);
        var periodStart = ComputePeriodStart(periodEnd, closingDay);
        var dueDate = ComputeDueDate(periodEnd, closingDay, dueDay);

        return (periodStart, periodEnd, dueDate);
    }

    /// <summary>
    /// Próxima fatura após uma fatura existente (parcelas 2..N).
    /// </summary>
    public static (DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate) NextStatement(
        DateOnly currentPeriodEnd,
        short closingDay,
        short dueDay)
    {
        var (nextYear, nextMonth) = AddMonths(currentPeriodEnd.Year, currentPeriodEnd.Month, 1);
        var newPeriodEnd = ClampDay(nextYear, nextMonth, closingDay);
        var newPeriodStart = ComputePeriodStart(newPeriodEnd, closingDay);
        var newDueDate = ComputeDueDate(newPeriodEnd, closingDay, dueDay);
        return (newPeriodStart, newPeriodEnd, newDueDate);
    }

    /// <summary>
    /// Distribui o total em N parcelas com 2 casas decimais. A última parcela
    /// recebe o resto pra que a soma exata bata. Ex: R$100 em 3x = [33.33, 33.33, 33.34].
    /// </summary>
    public static decimal[] DistributeAmounts(decimal total, short count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Number of installments must be positive.");
        }

        var per = Math.Round(total / count, 2, MidpointRounding.AwayFromZero);
        var amounts = new decimal[count];
        for (var i = 0; i < count - 1; i++)
        {
            amounts[i] = per;
        }
        // A última recebe o resto pra evitar erro de arredondamento
        amounts[count - 1] = total - (per * (count - 1));
        return amounts;
    }

    // ----- helpers de data -----

    private static DateOnly ComputePeriodStart(DateOnly periodEnd, short closingDay)
    {
        // Dia seguinte ao fechamento anterior
        var (prevYear, prevMonth) = AddMonths(periodEnd.Year, periodEnd.Month, -1);
        var previousClose = ClampDay(prevYear, prevMonth, closingDay);
        return previousClose.AddDays(1);
    }

    private static DateOnly ComputeDueDate(DateOnly periodEnd, short closingDay, short dueDay)
    {
        // Se due_day > closing_day: vencimento no MESMO mês do fechamento
        // (ex: closing 5, due 15 — fechou dia 5, vence dia 15)
        if (dueDay > closingDay)
        {
            return ClampDay(periodEnd.Year, periodEnd.Month, dueDay);
        }

        // Senão: vencimento no mês SEGUINTE ao fechamento
        // (ex: closing 25, due 10 — fechou dia 25, vence dia 10 do próximo mês)
        var (nextYear, nextMonth) = AddMonths(periodEnd.Year, periodEnd.Month, 1);
        return ClampDay(nextYear, nextMonth, dueDay);
    }

    /// <summary>
    /// Cria DateOnly garantindo que o dia não excede o último do mês
    /// (ex: dia 31 em fevereiro vira 28/29).
    /// </summary>
    private static DateOnly ClampDay(int year, int month, short day)
    {
        var maxDay = DateTime.DaysInMonth(year, month);
        var actualDay = Math.Min((int)day, maxDay);
        return new DateOnly(year, month, actualDay);
    }

    private static (int Year, int Month) AddMonths(int year, int month, int delta)
    {
        var totalMonths = ((year * 12) + (month - 1)) + delta;
        var newYear = totalMonths / 12;
        var newMonth = (totalMonths % 12) + 1;
        return (newYear, newMonth);
    }
}
