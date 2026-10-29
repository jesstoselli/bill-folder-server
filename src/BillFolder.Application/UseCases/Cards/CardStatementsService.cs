using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Abstractions.Time;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Cards;
using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Cards;

public class CardStatementsService
{
    private readonly IApplicationDbContext _db;
    private readonly IBusinessDateProvider _businessDate;
    private readonly IValidator<PayCardStatementRequest> _payValidator;

    public CardStatementsService(
        IApplicationDbContext db,
        IBusinessDateProvider businessDate,
        IValidator<PayCardStatementRequest> payValidator)
    {
        _db = db;
        _businessDate = businessDate;
        _payValidator = payValidator;
    }

    public async Task<List<CardStatementResponse>> ListAsync(
        Guid userId,
        Guid? cardId,
        CardStatementStatus? status,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var today = _businessDate.Today;

        var query = _db.CardStatements
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        if (cardId.HasValue)
        {
            query = query.Where(s => s.CardId == cardId.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(s => s.DueDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(s => s.DueDate <= to.Value);
        }

        var statements = await query
            .OrderByDescending(s => s.DueDate)
            .Select(s => new CardStatementResponse(
                s.Id,
                s.CardId,
                s.Card.Name,
                s.PeriodStart,
                s.PeriodEnd,
                s.DueDate,
                s.Status,
                s.PaidDate,
                s.ActualAmount,
                s.PaidFromAccountId,
                s.PaidFromAccount != null ? s.PaidFromAccount.BankName : null,
                s.Installments.Sum(i => i.Amount),
                s.Installments.Count,
                s.LinkedExpenseId,
                s.CreatedAt,
                s.UpdatedAt))
            .ToListAsync(ct);

        return statements
            .Where(s => !status.HasValue || CardStatementExtensions.MatchesEffectiveStatus(
                s.Status, s.PeriodEnd, today, status.Value))
            .Select(s => s with
            {
                Status = CardStatementExtensions.EffectiveStatus(s.Status, s.PeriodEnd, today),
            })
            .ToList();
    }

    public async Task<OperationResult<CardStatementDetailResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var statement = await _db.CardStatements
            .AsNoTracking()
            .Where(s => s.Id == id && s.UserId == userId)
            .Include(s => s.Card)
            .Include(s => s.PaidFromAccount)
            .Include(s => s.Installments).ThenInclude(i => i.CardEntry).ThenInclude(e => e.Category)
            .FirstOrDefaultAsync(ct);

        if (statement is null)
        {
            return OperationResult.Fail<CardStatementDetailResponse>(
                "not_found", "Fatura não encontrada.");
        }

        var installments = statement.Installments
            .OrderBy(i => i.CardEntry.PurchaseDate)
            .ThenBy(i => i.InstallmentNumber)
            .Select(i => new StatementInstallmentDto(
                i.Id,
                i.CardEntryId,
                i.InstallmentNumber,
                i.Amount,
                i.CardEntry.PurchaseDate,
                i.CardEntry.Label,
                i.CardEntry.Category.NamePt))
            .ToList();

        var totalAmount = installments.Sum(i => i.Amount);

        var today = _businessDate.Today;
        var response = new CardStatementDetailResponse(
            statement.Id,
            statement.CardId,
            statement.Card.Name,
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.DueDate,
            statement.EffectiveStatus(today),
            statement.PaidDate,
            statement.ActualAmount,
            statement.PaidFromAccountId,
            statement.PaidFromAccount?.BankName,
            totalAmount,
            statement.LinkedExpenseId,
            statement.CreatedAt,
            statement.UpdatedAt,
            installments);

        return OperationResult.Ok(response);
    }

    public async Task<OperationResult<CardStatementResponse>> PayAsync(
        Guid userId, Guid id, PayCardStatementRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _payValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CardStatementResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var statement = await _db.CardStatements
            .Include(s => s.Card)
            .Include(s => s.Installments)
            .Include(s => s.PaidFromAccount)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (statement is null)
        {
            return OperationResult.Fail<CardStatementResponse>(
                "not_found", "Fatura não encontrada.");
        }

        Guid? accountOwnerId = null;
        if (request.PaidFromAccountId.HasValue)
        {
            accountOwnerId = await _db.CheckingAccounts
                .Where(a => a.Id == request.PaidFromAccountId.Value && a.UserId == userId)
                .Select(a => (Guid?)a.UserId)
                .FirstOrDefaultAsync(ct);
        }

        var accountError = AccountError(userId, request.PaidFromAccountId, accountOwnerId);
        if (accountError is not null)
        {
            return OperationResult.Fail<CardStatementResponse>(
                accountError, "Conta não existe ou não pertence ao usuário.");
        }

        var today = _businessDate.Today;
        var paymentError = PaymentError(statement.Status, statement.PeriodEnd, today);
        if (paymentError is not null)
        {
            var message = paymentError == "statement_open"
                ? "Fatura ainda está aberta."
                : "Fatura já está paga.";
            return OperationResult.Fail<CardStatementResponse>(paymentError, message);
        }

        if (statement.Installments.Sum(i => i.Amount) <= 0m)
        {
            return OperationResult.Fail<CardStatementResponse>(
                "nothing_to_pay", "Fatura sem valor a pagar: os estornos cobrem as compras.");
        }

        ApplyPayment(statement, request);

        await _db.SaveChangesAsync(ct);

        var paidStatement = await _db.CardStatements
            .AsNoTracking()
            .Include(s => s.Card)
            .Include(s => s.Installments)
            .Include(s => s.PaidFromAccount)
            .FirstAsync(s => s.Id == statement.Id, ct);

        return OperationResult.Ok(MapToResponse(paidStatement, today));
    }

    internal static string? PaymentError(
        CardStatementStatus status, DateOnly periodEnd, DateOnly today) =>
        CardStatementExtensions.EffectiveStatus(status, periodEnd, today) switch
        {
            CardStatementStatus.Open => "statement_open",
            CardStatementStatus.Paid => "statement_paid",
            _ => null,
        };

    /// <summary>
    /// Desfaz o pagamento de uma fatura: ela volta a aceitar pagamento (fechada,
    /// pela data) e perde data, valor e conta do pagamento.
    /// </summary>
    public async Task<OperationResult<CardStatementResponse>> UndoPaymentAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var statement = await _db.CardStatements
            .Include(s => s.Card)
            .Include(s => s.Installments)
            .Include(s => s.PaidFromAccount)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (statement is null)
        {
            return OperationResult.Fail<CardStatementResponse>("not_found", "Fatura não encontrada.");
        }

        if (statement.Status != CardStatementStatus.Paid)
        {
            return OperationResult.Fail<CardStatementResponse>(
                "statement_not_paid", "Fatura não está paga.");
        }

        ClearPayment(statement);
        await _db.SaveChangesAsync(ct);

        var reopened = await _db.CardStatements
            .AsNoTracking()
            .Include(s => s.Card)
            .Include(s => s.Installments)
            .Include(s => s.PaidFromAccount)
            .FirstAsync(s => s.Id == statement.Id, ct);

        return OperationResult.Ok(MapToResponse(reopened, _businessDate.Today));
    }

    /// <summary>
    /// Sem pagamento. O status guardado volta a Open; o efetivo (fechada ou
    /// aberta) sai da data de fechamento, como antes de pagar.
    /// </summary>
    internal static void ClearPayment(CardStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        statement.Status = CardStatementStatus.Open;
        statement.PaidDate = null;
        statement.ActualAmount = null;
        statement.PaidFromAccountId = null;
    }

    internal static string? AccountError(
        Guid userId, Guid? requestedAccountId, Guid? accountOwnerId) =>
        !requestedAccountId.HasValue
            ? null
            : accountOwnerId != userId
                ? "invalid_account"
                : null;

    internal static void ApplyPayment(CardStatement statement, PayCardStatementRequest request)
    {
        statement.Status = CardStatementStatus.Paid;
        statement.PaidDate = request.PaidDate;
        statement.ActualAmount = request.ActualAmount;
        statement.PaidFromAccountId = request.PaidFromAccountId;
    }

    private static CardStatementResponse MapToResponse(CardStatement statement, DateOnly today) =>
        new(
            statement.Id,
            statement.CardId,
            statement.Card.Name,
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.DueDate,
            statement.EffectiveStatus(today),
            statement.PaidDate,
            statement.ActualAmount,
            statement.PaidFromAccountId,
            statement.PaidFromAccount?.BankName,
            statement.Installments.Sum(i => i.Amount),
            statement.Installments.Count,
            statement.LinkedExpenseId,
            statement.CreatedAt,
            statement.UpdatedAt);
}
