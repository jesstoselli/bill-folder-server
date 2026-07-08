using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Cards;
using BillFolder.Application.UseCases.Cards;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Endpoints;

public static class CardEntriesEndpoints
{
    public static IEndpointRouteBuilder MapCardEntriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/card-entries")
            .WithTags("CardEntries")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid? cardId,
            DateOnly? from,
            DateOnly? to,
            Guid? categoryId,
            ClaimsPrincipal principal,
            CardEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var list = await service.ListAsync(userId, cardId, from, to, categoryId, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            CardEntriesService service,
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
            CreateCardEntryRequest request,
            ClaimsPrincipal principal,
            CardEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/card-entries/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateCardEntryRequest request,
            ClaimsPrincipal principal,
            CardEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.UpdateAsync(userId, id, request, ct);
            return ToHttpResult(result);
        });

        // Reajusta o valor de uma assinatura (escopo no corpo).
        group.MapPost("/{id:guid}/update-amount", async (
            Guid id,
            UpdateCardSubscriptionAmountRequest request,
            ClaimsPrincipal principal,
            CardEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.UpdateSubscriptionAmountAsync(userId, id, request, ct);
            return ToHttpResult(result);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            string? scope,
            ClaimsPrincipal principal,
            CardEntriesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            // Query param parseado à mão (sem JsonStringEnumConverter): aceita
            // 'this'/'this_and_following' (case-insensitive), default This.
            var scopeEnum = RecurrenceScope.This;
            if (!string.IsNullOrWhiteSpace(scope))
            {
                if (!TryParseScope(scope, out scopeEnum))
                {
                    return Results.Json(
                        new
                        {
                            error = "invalid_scope",
                            message = "Scope deve ser 'this' ou 'this_and_following'.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }
            }

            var result = await service.DeleteAsync(userId, id, scopeEnum, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : ToHttpResult(result);
        });

        return app;
    }

    // Aceita 'this' e 'this_and_following' (snake_case, case-insensitive), além
    // dos nomes do enum. Enum.TryParse não casa o underscore sozinho.
    private static bool TryParseScope(string value, out RecurrenceScope scope)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "this":
                scope = RecurrenceScope.This;
                return true;
            case "this_and_following":
            case "thisandfollowing":
                scope = RecurrenceScope.ThisAndFollowing;
                return true;
            default:
                scope = RecurrenceScope.This;
                return false;
        }
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
            "invalid_card"     => StatusCodes.Status400BadRequest,
            "invalid_category" => StatusCodes.Status400BadRequest,
            "not_found"        => StatusCodes.Status404NotFound,
            _                  => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
