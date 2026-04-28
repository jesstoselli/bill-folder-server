using BillFolder.Domain.Enums;

namespace BillFolder.Application.Dtos.CycleAdjustments;

public sealed record CycleAdjustmentResponse(
    Guid Id,
    CycleAdjustmentType Type,
    string Label,
    decimal Amount,
    DateOnly Date,
    Guid? SourceSavingsTransactionId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCycleAdjustmentRequest(
    CycleAdjustmentType Type,
    string Label,
    decimal Amount,
    DateOnly Date,
    Guid? SourceSavingsTransactionId);

public sealed record UpdateCycleAdjustmentRequest(
    CycleAdjustmentType? Type,
    string? Label,
    decimal? Amount,
    DateOnly? Date,
    Guid? SourceSavingsTransactionId);
