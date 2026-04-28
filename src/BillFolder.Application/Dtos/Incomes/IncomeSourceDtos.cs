using BillFolder.Domain.Enums;

namespace BillFolder.Application.Dtos.Incomes;

public sealed record IncomeSourceResponse(
    Guid Id,
    string Origin,
    IncomeOriginType OriginType,
    decimal DefaultAmount,
    short ExpectedDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateIncomeSourceRequest(
    string Origin,
    IncomeOriginType OriginType,
    decimal DefaultAmount,
    short ExpectedDay,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record UpdateIncomeSourceRequest(
    string? Origin,
    IncomeOriginType? OriginType,
    decimal? DefaultAmount,
    short? ExpectedDay,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? IsActive);
