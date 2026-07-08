using BillFolder.Application.UseCases.Cards;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Cards;

/// <summary>
/// Ao excluir uma assinatura de cartão (CardEntry mensal com TemplateId), o escopo
/// decide o conjunto atingido: só a ocorrência-alvo, ou ela + as seguintes. Ocorrências
/// cuja fatura já está fechada/paga (Closed/Paid) nunca são apagadas — faturas passadas
/// permanecem intactas. Helper puro (sem DB): o status da fatura de cada entry entra por
/// um dicionário, mantendo o helper testável.
/// </summary>
public class SubscriptionScopeDeleteTests
{
    private static readonly Guid TemplateId = Guid.NewGuid();

    private static CardEntry Occurrence(DateOnly purchaseDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            TemplateId = TemplateId,
            PurchaseDate = purchaseDate,
            InstallmentsCount = 1,
        };

    [Fact]
    public void This_returns_only_the_target()
    {
        var jun = Occurrence(new DateOnly(2026, 6, 10));
        var jul = Occurrence(new DateOnly(2026, 7, 10));
        var ago = Occurrence(new DateOnly(2026, 8, 10));

        var status = new Dictionary<Guid, CardStatementStatus>
        {
            [jun.Id] = CardStatementStatus.Open,
            [jul.Id] = CardStatementStatus.Open,
            [ago.Id] = CardStatementStatus.Open,
        };

        var ids = CardEntriesService.SubscriptionOccurrencesToDelete(
            new[] { jun, jul, ago }, target: jul, scope: RecurrenceScope.This, statementStatusByEntryId: status);

        Assert.Equal(new[] { jul.Id }, ids);
    }

    [Fact]
    public void ThisAndFollowing_returns_target_plus_later_open_occurrences()
    {
        var jun = Occurrence(new DateOnly(2026, 6, 10));
        var jul = Occurrence(new DateOnly(2026, 7, 10));
        var ago = Occurrence(new DateOnly(2026, 8, 10));

        var status = new Dictionary<Guid, CardStatementStatus>
        {
            [jun.Id] = CardStatementStatus.Open,
            [jul.Id] = CardStatementStatus.Open,
            [ago.Id] = CardStatementStatus.Open,
        };

        var ids = CardEntriesService.SubscriptionOccurrencesToDelete(
            new[] { jun, jul, ago }, target: jul, scope: RecurrenceScope.ThisAndFollowing, statementStatusByEntryId: status);

        Assert.Equal(new HashSet<Guid> { jul.Id, ago.Id }, ids.ToHashSet());
        Assert.DoesNotContain(jun.Id, ids);
    }

    [Fact]
    public void ThisAndFollowing_excludes_closed_and_paid_future_occurrences()
    {
        var jul = Occurrence(new DateOnly(2026, 7, 10));
        var agoClosed = Occurrence(new DateOnly(2026, 8, 10));
        var setPaid = Occurrence(new DateOnly(2026, 9, 10));
        var outOpen = Occurrence(new DateOnly(2026, 10, 10));

        var status = new Dictionary<Guid, CardStatementStatus>
        {
            [jul.Id] = CardStatementStatus.Open,
            [agoClosed.Id] = CardStatementStatus.Closed,
            [setPaid.Id] = CardStatementStatus.Paid,
            [outOpen.Id] = CardStatementStatus.Open,
        };

        var ids = CardEntriesService.SubscriptionOccurrencesToDelete(
            new[] { jul, agoClosed, setPaid, outOpen }, target: jul, scope: RecurrenceScope.ThisAndFollowing, statementStatusByEntryId: status);

        Assert.Equal(new HashSet<Guid> { jul.Id, outOpen.Id }, ids.ToHashSet());
        Assert.DoesNotContain(agoClosed.Id, ids);
        Assert.DoesNotContain(setPaid.Id, ids);
    }
}
