namespace BillFolder.Application.UseCases.Recurrences;

/// <summary>
/// Helpers de data compartilhados pelos motores de recorrência mensal (cartão
/// e despesa). Ciclos do BillFolder são ~mensais mas podem cruzar a fronteira
/// do mês (ex: 18/jun–17/jul), então tentamos o mês do start e o do end.
/// </summary>
public static class RecurrenceDates
{
    /// <summary>
    /// A data em [start, end] correspondente ao <paramref name="dayOfMonth"/>,
    /// clampado ao último dia do mês (ex: dia 31 em fevereiro vira 28/29).
    /// Tenta o mês do <paramref name="start"/> e, se não cair no range, o mês
    /// do <paramref name="end"/>. Retorna null se não cai em nenhum dos dois.
    /// </summary>
    public static DateOnly? MonthlyDateInRange(DateOnly start, DateOnly end, int dayOfMonth)
    {
        if (start > end)
        {
            return null;
        }

        var candidate = ClampedDate(start.Year, start.Month, dayOfMonth);
        if (candidate >= start && candidate <= end)
        {
            return candidate;
        }

        if (end.Year != start.Year || end.Month != start.Month)
        {
            candidate = ClampedDate(end.Year, end.Month, dayOfMonth);
            if (candidate >= start && candidate <= end)
            {
                return candidate;
            }
        }

        return null;
    }

    private static DateOnly ClampedDate(int year, int month, int dayOfMonth)
    {
        var maxDay = DateTime.DaysInMonth(year, month);
        var day = Math.Min(dayOfMonth, maxDay);
        return new DateOnly(year, month, day);
    }
}
