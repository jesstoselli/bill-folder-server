using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Savings;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Savings;

public class SavingsAccountsService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateSavingsAccountRequest> _createValidator;
    private readonly IValidator<UpdateSavingsAccountRequest> _updateValidator;

    public SavingsAccountsService(
        IApplicationDbContext db,
        IValidator<CreateSavingsAccountRequest> createValidator,
        IValidator<UpdateSavingsAccountRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<SavingsAccountResponse>> ListAsync(
        Guid userId, CancellationToken ct = default)
    {
        var accounts = await _db.SavingsAccounts
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.BankName)
            .ToListAsync(ct);

        if (accounts.Count == 0)
        {
            return [];
        }

        // Uma query flat de todas as transações do user, agrupada em memória
        // por conta (volume modesto num app pessoal; evita o quirk de
        // Include+Sum aninhado do EF).
        var txnsByAccount = (await _db.SavingsTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .ToListAsync(ct))
            .GroupBy(t => t.SavingsAccountId)
            .ToDictionary(g => g.Key, g => (IEnumerable<SavingsTransaction>)g.ToList());

        return accounts
            .Select(a => MapToResponse(
                a,
                SavingsBalance.Compute(
                    a.InitialBalance,
                    txnsByAccount.GetValueOrDefault(a.Id, Array.Empty<SavingsTransaction>()))))
            .ToList();
    }

    public async Task<OperationResult<SavingsAccountResponse>> GetAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var account = await _db.SavingsAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (account is null)
        {
            return OperationResult.Fail<SavingsAccountResponse>("not_found", "Poupança não encontrada.");
        }

        var balance = await ComputeBalanceAsync(account.Id, account.InitialBalance, ct);
        return OperationResult.Ok(MapToResponse(account, balance));
    }

    public async Task<OperationResult<SavingsAccountResponse>> CreateAsync(
        Guid userId, CreateSavingsAccountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<SavingsAccountResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        // Checking account precisa pertencer ao user
        var checkingOwnedByUser = await _db.CheckingAccounts
            .AnyAsync(c => c.Id == request.CheckingAccountId && c.UserId == userId, ct);
        if (!checkingOwnedByUser)
        {
            return OperationResult.Fail<SavingsAccountResponse>(
                "invalid_checking_account",
                "Conta corrente não existe ou não pertence ao usuário.");
        }

        // 1:1 → não pode ter mais de uma savings vinculada à mesma checking
        var alreadyHasSavings = await _db.SavingsAccounts
            .AnyAsync(s => s.CheckingAccountId == request.CheckingAccountId, ct);
        if (alreadyHasSavings)
        {
            return OperationResult.Fail<SavingsAccountResponse>(
                "checking_already_has_savings",
                "Essa conta corrente já tem uma poupança vinculada.");
        }

        var account = new SavingsAccount
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CheckingAccountId = request.CheckingAccountId,
            BankName = request.BankName.Trim(),
            Branch = request.Branch.Trim(),
            AccountNumber = request.AccountNumber.Trim(),
            InitialBalance = request.InitialBalance,
        };

        _db.SavingsAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        // Conta recém-criada não tem transações → saldo = InitialBalance.
        return OperationResult.Ok(MapToResponse(account, account.InitialBalance));
    }

    public async Task<OperationResult<SavingsAccountResponse>> UpdateAsync(
        Guid userId, Guid id, UpdateSavingsAccountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<SavingsAccountResponse>(
                "validation_error", validation.Errors[0].ErrorMessage);
        }

        var account = await _db.SavingsAccounts
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (account is null)
        {
            return OperationResult.Fail<SavingsAccountResponse>(
                "not_found", "Poupança não encontrada.");
        }

        if (request.BankName is not null)
        {
            account.BankName = request.BankName.Trim();
        }
        if (request.Branch is not null)
        {
            account.Branch = request.Branch.Trim();
        }
        if (request.AccountNumber is not null)
        {
            account.AccountNumber = request.AccountNumber.Trim();
        }
        if (request.InitialBalance.HasValue)
        {
            account.InitialBalance = request.InitialBalance.Value;
        }

        await _db.SaveChangesAsync(ct);

        var balance = await ComputeBalanceAsync(account.Id, account.InitialBalance, ct);
        return OperationResult.Ok(MapToResponse(account, balance));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid userId, Guid id, CancellationToken ct = default)
    {
        var account = await _db.SavingsAccounts
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (account is null)
        {
            return OperationResult.Fail<bool>("not_found", "Poupança não encontrada.");
        }

        _db.SavingsAccounts.Remove(account);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    private static SavingsAccountResponse MapToResponse(SavingsAccount s, decimal currentBalance) =>
        new(s.Id, s.CheckingAccountId, s.BankName, s.Branch!, s.AccountNumber!,
            s.InitialBalance, currentBalance, s.CreatedAt, s.UpdatedAt);

    /// <summary>Carrega as transações da conta e calcula o saldo corrente.</summary>
    private async Task<decimal> ComputeBalanceAsync(
        Guid accountId, decimal initialBalance, CancellationToken ct)
    {
        var txns = await _db.SavingsTransactions
            .AsNoTracking()
            .Where(t => t.SavingsAccountId == accountId)
            .ToListAsync(ct);
        return SavingsBalance.Compute(initialBalance, txns);
    }
}
