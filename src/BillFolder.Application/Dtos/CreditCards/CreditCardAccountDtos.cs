namespace BillFolder.Application.Dtos.CreditCards;

public sealed record CreditCardAccountResponse(
    Guid Id,
    string Name,
    string? IssuerBank,
    string? Brand,
    short ClosingDay,
    short DueDay,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCreditCardAccountRequest(
    string Name,
    string? IssuerBank,
    string? Brand,
    short ClosingDay,
    short DueDay);

public sealed record UpdateCreditCardAccountRequest(
    string? Name,
    string? IssuerBank,
    string? Brand,
    short? ClosingDay,
    short? DueDay);
