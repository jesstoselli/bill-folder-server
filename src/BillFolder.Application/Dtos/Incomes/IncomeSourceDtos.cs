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

/// <summary>
/// Partial update: a null field keeps its current value. Since a null
/// <see cref="EndDate"/> therefore means "unchanged", removing the end date
/// takes an explicit <see cref="ClearEndDate"/> = true.
/// </summary>
public sealed record UpdateIncomeSourceRequest(
    string? Origin,
    IncomeOriginType? OriginType,
    decimal? DefaultAmount,
    short? ExpectedDay,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? IsActive,
    bool? ClearEndDate = null);
