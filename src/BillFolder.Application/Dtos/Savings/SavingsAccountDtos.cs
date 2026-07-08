namespace BillFolder.Application.Dtos.Savings;

public sealed record SavingsAccountResponse(
    Guid Id,
    Guid CheckingAccountId,
    string BankName,
    string Branch,
    string AccountNumber,
    decimal InitialBalance,
    // Saldo corrente = InitialBalance + Σ transações com sinal (SavingsBalance).
    decimal CurrentBalance,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateSavingsAccountRequest(
    Guid CheckingAccountId,
    string BankName,
    string Branch,
    string AccountNumber,
    decimal InitialBalance);

public sealed record UpdateSavingsAccountRequest(
    string? BankName,
    string? Branch,
    string? AccountNumber,
    decimal? InitialBalance);
