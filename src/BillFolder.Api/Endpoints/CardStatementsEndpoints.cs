using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Cards;
using BillFolder.Application.UseCases.Cards;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Endpoints;

public static class CardStatementsEndpoints
{
    public static IEndpointRouteBuilder MapCardStatementsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/card-statements")
            .WithTags("CardStatements")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid? cardId,
            string? status,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal principal,
            CardStatementsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            CardStatementStatus? statusEnum = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<CardStatementStatus>(status, ignoreCase: true, out var parsed))
                {
                    return Results.Json(
                        new
                        {
                            error = "invalid_status",
                            message = "Status deve ser 'open', 'closed' ou 'paid'.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }
                statusEnum = parsed;
            }

            var list = await service.ListAsync(userId, cardId, statusEnum, from, to, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            CardStatementsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.GetAsync(userId, id, ct);
            return ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateCardStatementRequest request,
            ClaimsPrincipal principal,
            CardStatementsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.UpdateStatusAsync(userId, id, request, ct);
            return ToHttpResult(result);
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
            "not_found"        => StatusCodes.Status404NotFound,
            _                  => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
