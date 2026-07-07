namespace BillFolder.Domain.Enums;

/// <summary>
/// Cadência de uma ExpenseRecurrence.
///  - Monthly: template legado (dia do mês via DueDay). Dormente por enquanto.
///  - Weekly: despesa provisionada paga semanalmente (terapia, diarista). Gera
///    UMA despesa por ciclo cujo valor cheio = DefaultAmount × nº de ocorrências
///    do Weekday no ciclo, quitada uma ocorrência (semana) por vez.
/// </summary>
public enum ExpenseRecurrenceFrequency
{
    Monthly,
    Weekly
}
