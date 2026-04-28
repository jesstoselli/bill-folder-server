namespace BillFolder.Application.Dtos.Accounts;

public sealed record CheckingAccountResponse(
    Guid Id,
    string BankName,
    string? Branch,
    string? AccountNumber,
    decimal InitialBalance,
    bool IsPrimary,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCheckingAccountRequest(
    string BankName,
    string Branch,
    string AccountNumber,
    decimal InitialBalance,
    bool IsPrimary);

/// <summary>
/// PATCH parcial — todos os campos opcionais. Cliente envia só o que quer mudar.
/// </summary>
public sealed record UpdateCheckingAccountRequest(
    string? BankName,
    string? Branch,
    string? AccountNumber,
    decimal? InitialBalance,
    bool? IsPrimary);
