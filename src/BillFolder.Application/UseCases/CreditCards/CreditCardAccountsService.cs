using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.CreditCards;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.CreditCards;

public class CreditCardAccountsService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateCreditCardAccountRequest> _createValidator;
    private readonly IValidator<UpdateCreditCardAccountRequest> _updateValidator;

    public CreditCardAccountsService(
        IApplicationDbContext db,
        IValidator<CreateCreditCardAccountRequest> createValidator,
        IValidator<UpdateCreditCardAccountRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<CreditCardAccountResponse>> ListAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await _db.CreditCardAccounts
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .Select(c => new CreditCardAccountResponse(
                c.Id, c.Name, c.IssuerBank, c.Brand,
                c.ClosingDay, c.DueDay, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<CreditCardAccountResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var card = await _db.CreditCardAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        return card is null
            ? OperationResult.Fail<CreditCardAccountResponse>("not_found", "Cartão não encontrado.")
            : OperationResult.Ok(MapToResponse(card));
    }

    public async Task<OperationResult<CreditCardAccountResponse>> CreateAsync(
        Guid userId, CreateCreditCardAccountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CreditCardAccountResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var card = new CreditCardAccount
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = request.Name.Trim(),
            IssuerBank = NormalizeOptional(request.IssuerBank),
            Brand = NormalizeOptional(request.Brand),
            ClosingDay = request.ClosingDay,
            DueDay = request.DueDay,
        };

        _db.CreditCardAccounts.Add(card);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(card));
    }

    public async Task<OperationResult<CreditCardAccountResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateCreditCardAccountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CreditCardAccountResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var card = await _db.CreditCardAccounts
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (card is null)
        {
            return OperationResult.Fail<CreditCardAccountResponse>(
                "not_found", "Cartão não encontrado.");
        }

        if (request.Name is not null)
        {
            card.Name = request.Name.Trim();
        }
        if (request.IssuerBank is not null)
        {
            card.IssuerBank = NormalizeOptional(request.IssuerBank);
        }
        if (request.Brand is not null)
        {
            card.Brand = NormalizeOptional(request.Brand);
        }
        if (request.ClosingDay.HasValue)
        {
            card.ClosingDay = request.ClosingDay.Value;
        }
        if (request.DueDay.HasValue)
        {
            card.DueDay = request.DueDay.Value;
        }

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(card));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var card = await _db.CreditCardAccounts
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (card is null)
        {
            return OperationResult.Fail<bool>("not_found", "Cartão não encontrado.");
        }

        // FK em card_statements/card_entries com ON DELETE CASCADE — o banco apaga
        // automaticamente fatura e compras vinculados. Atenção: dados históricos vão junto.
        _db.CreditCardAccounts.Remove(card);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    // ----- helpers -----

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }
        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CreditCardAccountResponse MapToResponse(CreditCardAccount c) =>
        new(c.Id, c.Name, c.IssuerBank, c.Brand,
            c.ClosingDay, c.DueDay, c.CreatedAt, c.UpdatedAt);
}
