using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.CycleAdjustments;
using BillFolder.Application.UseCases.CycleAdjustments;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Endpoints;

public static class CycleAdjustmentsEndpoints
{
    public static IEndpointRouteBuilder MapCycleAdjustmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/cycle-adjustments")
            .WithTags("CycleAdjustments")
            .RequireAuthorization();

        group.MapGet("/", async (
            DateOnly? from,
            DateOnly? to,
            string? type,
            ClaimsPrincipal principal,
            CycleAdjustmentsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            CycleAdjustmentType? typeEnum = null;
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (!Enum.TryParse<CycleAdjustmentType>(type, ignoreCase: true, out var parsed))
                {
                    return Results.Json(
                        new
                        {
                            error = "invalid_type",
                            message = "Tipo deve ser 'inflow' ou 'outflow'.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }
                typeEnum = parsed;
            }

            var list = await service.ListAsync(userId, from, to, typeEnum, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            CycleAdjustmentsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            return ToHttpResult(await service.GetAsync(userId, id, ct));
        });

        group.MapPost("/", async (
            CreateCycleAdjustmentRequest request,
            ClaimsPrincipal principal,
            CycleAdjustmentsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/cycle-adjustments/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateCycleAdjustmentRequest request,
            ClaimsPrincipal principal,
            CycleAdjustmentsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            return ToHttpResult(await service.UpdateAsync(userId, id, request, ct));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            CycleAdjustmentsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.DeleteAsync(userId, id, ct);
            return result.IsSuccess ? Results.NoContent() : ToHttpResult(result);
        });

        return app;
    }

    private static IResult ToHttpResult<T>(OperationResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }
        var status = result.ErrorCode switch
        {
            "validation_error"            => StatusCodes.Status400BadRequest,
            "invalid_source_transaction"  => StatusCodes.Status400BadRequest,
            "not_found"                   => StatusCodes.Status404NotFound,
            _                             => StatusCodes.Status400BadRequest,
        };
        return Results.Json(new { error = result.ErrorCode, message = result.ErrorMessage }, statusCode: status);
    }
}
