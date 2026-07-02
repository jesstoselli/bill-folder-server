namespace BillFolder.Application.Dtos.Cycles;

public sealed record CycleResponse(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    string Label,
    bool IsRecurrenceGenerated,
    bool IsCurrent,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCycleRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string Label);

/// <summary>
/// Resposta do endpoint one-shot POST /v1/cycles/backfill-window.
/// GeneratedCount = quantos ciclos novos foram criados nessa chamada
/// (0 se a janela já estava completa). Idempotente.
/// </summary>
public sealed record BackfillCyclesResponse(int GeneratedCount);

/// <summary>
/// PATCH parcial. Se ambos start/end são enviados, a validação cross-field
/// garante start &lt; end. Se só um é enviado, o service valida contra o existente.
/// </summary>
public sealed record UpdateCycleRequest(
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Label);
