using BillFolder.Domain.Enums;

namespace BillFolder.Application.Dtos.Incomes;

public sealed record IncomeEntryResponse(
    Guid Id,
    Guid? SourceId,
    string? SourceOrigin,
    decimal ExpectedAmount,
    decimal? ActualAmount,
    DateOnly ExpectedDate,
    DateOnly? ActualDate,
    IncomeStatus Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateIncomeEntryRequest(
    Guid? SourceId,
    decimal ExpectedAmount,
    DateOnly ExpectedDate,
    string? Notes);

public sealed record UpdateIncomeEntryRequest(
    Guid? SourceId,
    decimal? ExpectedAmount,
    decimal? ActualAmount,
    DateOnly? ExpectedDate,
    DateOnly? ActualDate,
    IncomeStatus? Status,
    string? Notes);
