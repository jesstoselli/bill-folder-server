using BillFolder.Application.Dtos.Cards;
using BillFolder.Application.UseCases.Cards;
using BillFolder.Application.Validators.Cards;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Tests.Cards;

public class CardStatementPaymentTests
{
    [Fact]
    public void PaymentError_returns_statement_open_before_period_end()
    {
        var error = CardStatementsService.PaymentError(
            CardStatementStatus.Open,
            periodEnd: new DateOnly(2026, 8, 17),
            today: new DateOnly(2026, 8, 16));

        Assert.Equal("statement_open", error);
    }

    [Fact]
    public void PaymentError_allows_effectively_closed_statement()
    {
        var error = CardStatementsService.PaymentError(
            CardStatementStatus.Open,
            periodEnd: new DateOnly(2026, 8, 17),
            today: new DateOnly(2026, 8, 17));

        Assert.Null(error);
    }

    [Fact]
    public void PaymentError_rejects_paid_statement()
    {
        var error = CardStatementsService.PaymentError(
            CardStatementStatus.Paid,
            periodEnd: new DateOnly(2026, 7, 17),
            today: new DateOnly(2026, 8, 1));

        Assert.Equal("statement_paid", error);
    }

    [Fact]
    public void AccountError_allows_no_account_requested()
    {
        var error = CardStatementsService.AccountError(
            userId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            requestedAccountId: null,
            accountOwnerId: null);

        Assert.Null(error);
    }

    [Fact]
    public void AccountError_rejects_requested_account_not_found()
    {
        var error = CardStatementsService.AccountError(
            userId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            requestedAccountId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            accountOwnerId: null);

        Assert.Equal("invalid_account", error);
    }

    [Fact]
    public void AccountError_rejects_account_from_another_user()
    {
        var error = CardStatementsService.AccountError(
            userId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            requestedAccountId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            accountOwnerId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        Assert.Equal("invalid_account", error);
    }

    [Fact]
    public void ApplyPayment_persists_all_payment_fields_on_entity()
    {
        var statement = new CardStatement { Status = CardStatementStatus.Open };
        var request = new PayCardStatementRequest(
            new DateOnly(2026, 8, 1),
            790m,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

        CardStatementsService.ApplyPayment(statement, request);

        Assert.Equal(CardStatementStatus.Paid, statement.Status);
        Assert.Equal(request.PaidDate, statement.PaidDate);
        Assert.Equal(request.ActualAmount, statement.ActualAmount);
        Assert.Equal(request.PaidFromAccountId, statement.PaidFromAccountId);
    }

    [Fact]
    public void Validator_rejects_default_paid_date()
    {
        var result = new PayCardStatementRequestValidator().Validate(
            new PayCardStatementRequest(default, 790m, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_non_positive_amount()
    {
        var result = new PayCardStatementRequestValidator().Validate(
            new PayCardStatementRequest(new DateOnly(2026, 8, 1), 0m, null));

        Assert.False(result.IsValid);
    }
}
