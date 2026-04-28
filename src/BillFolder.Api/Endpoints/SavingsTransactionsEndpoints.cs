using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Savings;
using BillFolder.Application.UseCases.Savings;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Endpoints;

public static class SavingsTransactionsEndpoints
{
    public static IEndpointRouteBuilder MapSavingsTransactionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/savings-transactions")
            .WithTags("SavingsTransactions")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid? savingsAccountId,
            DateOnly? from,
            DateOnly? to,
            string? type,
            ClaimsPrincipal principal,
            SavingsTransactionsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            SavingsTransactionType? typeEnum = null;
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (!Enum.TryParse<SavingsTransactionType>(type, ignoreCase: true, out var parsed))
                {
                    return Results.Json(
                        new
                        {
                            error = "invalid_type",
                            message = "Tipo deve ser 'deposit', 'withdrawal', 'yield', 'transferIn' ou 'transferOut'.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }
                typeEnum = parsed;
            }

            var list = await service.ListAsync(userId, savingsAccountId, from, to, typeEnum, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            SavingsTransactionsService service,
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
            CreateSavingsTransactionRequest request,
            ClaimsPrincipal principal,
            SavingsTransactionsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/savings-transactions/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateSavingsTransactionRequest request,
            ClaimsPrincipal principal,
            SavingsTransactionsService service,
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
            SavingsTransactionsService service,
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
            "validation_error"        => StatusCodes.Status400BadRequest,
            "invalid_savings_account" => StatusCodes.Status400BadRequest,
            "not_found"               => StatusCodes.Status404NotFound,
            _                         => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
