using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Incomes;
using BillFolder.Application.UseCases.Incomes;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Endpoints;

public static class IncomeEntriesEndpoints
{
    public static IEndpointRouteBuilder MapIncomeEntriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/income-entries")
            .WithTags("IncomeEntries")
            .RequireAuthorization();

        group.MapGet("/", async (
            DateOnly? from,
            DateOnly? to,
            string? status,
            Guid? sourceId,
            ClaimsPrincipal principal,
            IncomeEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            // Query params não usam JsonStringEnumConverter — parse manual
            IncomeStatus? statusEnum = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<IncomeStatus>(status, ignoreCase: true, out var parsed))
                {
                    return Results.Json(
                        new
                        {
                            error = "invalid_status",
                            message = "Status deve ser 'expected', 'received', 'late' ou 'notOccurred'.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }
                statusEnum = parsed;
            }

            var list = await service.ListAsync(userId, from, to, statusEnum, sourceId, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IncomeEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.GetAsync(userId, id, ct);
            return ToHttpResult(result);
        });

        group.MapPost("/", async (
            CreateIncomeEntryRequest request,
            ClaimsPrincipal principal,
            IncomeEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/income-entries/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateIncomeEntryRequest request,
            ClaimsPrincipal principal,
            IncomeEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.UpdateAsync(userId, id, request, ct);
            return ToHttpResult(result);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IncomeEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.DeleteAsync(userId, id, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : ToHttpResult(result);
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
            "validation_error" => StatusCodes.Status400BadRequest,
            "invalid_source"   => StatusCodes.Status400BadRequest,
            "not_found"        => StatusCodes.Status404NotFound,
            _                  => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
