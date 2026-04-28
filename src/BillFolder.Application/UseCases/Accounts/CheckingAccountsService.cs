using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Accounts;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Accounts;

public class CheckingAccountsService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateCheckingAccountRequest> _createValidator;
    private readonly IValidator<UpdateCheckingAccountRequest> _updateValidator;

    public CheckingAccountsService(
        IApplicationDbContext db,
        IValidator<CreateCheckingAccountRequest> createValidator,
        IValidator<UpdateCheckingAccountRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<CheckingAccountResponse>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.CheckingAccounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => a.BankName)
            .Select(a => new CheckingAccountResponse(
                a.Id, a.BankName, a.Branch, a.AccountNumber,
                a.InitialBalance, a.IsPrimary, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationResult<CheckingAccountResponse>> GetAsync(
        Guid userId, Guid accountId, CancellationToken ct = default)
    {
        var account = await _db.CheckingAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, ct);

        return account is null
            ? OperationResult.Fail<CheckingAccountResponse>("not_found", "Conta não encontrada.")
            : OperationResult.Ok(MapToResponse(account));
    }

    public async Task<OperationResult<CheckingAccountResponse>> CreateAsync(
        Guid userId, CreateCheckingAccountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CheckingAccountResponse>(
                "validation_error",
                validation.Errors[0].ErrorMessage);
        }

        // Se essa conta tá sendo marcada como primary, desmarca outras do mesmo user
        if (request.IsPrimary)
        {
            await UnsetOtherPrimariesAsync(userId, exceptId: null, ct);
        }

        var account = new CheckingAccount
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BankName = request.BankName.Trim(),
            Branch = request.Branch.Trim(),
            AccountNumber = request.AccountNumber.Trim(),
            InitialBalance = request.InitialBalance,
            IsPrimary = request.IsPrimary,
        };

        _db.CheckingAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(account));
    }

    public async Task<OperationResult<CheckingAccountResponse>> UpdateAsync(
        Guid userId, Guid accountId, UpdateCheckingAccountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<CheckingAccountResponse>(
                "validation_error",
                validation.Errors[0].ErrorMessage);
        }

        var account = await _db.CheckingAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, ct);

        if (account is null)
        {
            return OperationResult.Fail<CheckingAccountResponse>("not_found", "Conta não encontrada.");
        }

        // Patch parcial: só atualiza os campos enviados
        if (request.BankName is not null)
        {
            account.BankName = request.BankName.Trim();
        }
        if (request.Branch is not null)
        {
            account.Branch = NormalizeOptional(request.Branch);
        }
        if (request.AccountNumber is not null)
        {
            account.AccountNumber = NormalizeOptional(request.AccountNumber);
        }
        if (request.InitialBalance.HasValue)
        {
            account.InitialBalance = request.InitialBalance.Value;
        }

        // is_primary lógica especial: garantir o invariant "max 1 primary per user"
        if (request.IsPrimary == true && !account.IsPrimary)
        {
            await UnsetOtherPrimariesAsync(userId, exceptId: accountId, ct);
            account.IsPrimary = true;
        }
        else if (request.IsPrimary == false && account.IsPrimary)
        {
            account.IsPrimary = false;
        }

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(MapToResponse(account));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid accountId, CancellationToken ct = default)
    {
        var account = await _db.CheckingAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, ct);

        if (account is null)
        {
            return OperationResult.Fail<bool>("not_found", "Conta não encontrada.");
        }

        _db.CheckingAccounts.Remove(account);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    // ----- helpers -----

    private async Task UnsetOtherPrimariesAsync(Guid userId, Guid? exceptId, CancellationToken ct)
    {
        var query = _db.CheckingAccounts.Where(a => a.UserId == userId && a.IsPrimary);
        if (exceptId.HasValue)
        {
            query = query.Where(a => a.Id != exceptId.Value);
        }

        var others = await query.ToListAsync(ct);
        foreach (var other in others)
        {
            other.IsPrimary = false;
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }
        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CheckingAccountResponse MapToResponse(CheckingAccount a) =>
        new(a.Id, a.BankName, a.Branch, a.AccountNumber,
            a.InitialBalance, a.IsPrimary, a.CreatedAt, a.UpdatedAt);
}
