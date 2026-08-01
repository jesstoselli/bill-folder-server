using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Application.UseCases.Cards;

public static class CardStatementExtensions
{
    public static CardStatementStatus EffectiveStatus(this CardStatement statement, DateOnly today) =>
        EffectiveStatus(statement.Status, statement.PeriodEnd, today);

    public static CardStatementStatus EffectiveStatus(
        CardStatementStatus persistedStatus,
        DateOnly periodEnd,
        DateOnly today) =>
        persistedStatus == CardStatementStatus.Paid
            ? CardStatementStatus.Paid
            : today >= periodEnd
                ? CardStatementStatus.Closed
                : CardStatementStatus.Open;

    public static bool MatchesEffectiveStatus(
        CardStatementStatus persistedStatus,
        DateOnly periodEnd,
        DateOnly today,
        CardStatementStatus requested) =>
        EffectiveStatus(persistedStatus, periodEnd, today) == requested;
}
