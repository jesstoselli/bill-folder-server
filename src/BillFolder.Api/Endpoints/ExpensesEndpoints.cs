using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Expenses;
using BillFolder.Application.UseCases.Expenses;
using BillFolder.Domain.Enums;

namespace BillFolder.Api.Endpoints;

public static class ExpensesEndpoints
{
    public static IEndpointRouteBuilder MapExpensesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/expenses")
            .WithTags("Expenses")
            .RequireAuthorization();

        group.MapGet("/", async (
            DateOnly? from,
            DateOnly? to,
            string? status,
            Guid? categoryId,
            ClaimsPrincipal principal,
            ExpensesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            // Query params não usam JsonStringEnumConverter — parseamos à mão pra
            // aceitar 'pending'/'paid'/'overdue' (case-insensitive) e dar erro 400 amigável
            ExpenseStatus? statusEnum = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<ExpenseStatus>(status, ignoreCase: true, out var parsed))
                {
                    return Results.Json(
                        new
                        {
                            error = "invalid_status",
                            message = "Status deve ser 'pending', 'paid' ou 'overdue'.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }
                statusEnum = parsed;
            }

            var list = await service.ListAsync(userId, from, to, statusEnum, categoryId, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            ExpensesService service,
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
            CreateExpenseRequest request,
            ClaimsPrincipal principal,
            ExpensesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/expenses/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateExpenseRequest request,
            ClaimsPrincipal principal,
            ExpensesService service,
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
            ExpensesService service,
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
            "invalid_account"  => StatusCodes.Status400BadRequest,
            "invalid_category" => StatusCodes.Status400BadRequest,
            "not_found"        => StatusCodes.Status404NotFound,
            _                  => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
