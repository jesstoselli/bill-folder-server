using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.DailyExpenses;
using BillFolder.Application.UseCases.DailyExpenses;

namespace BillFolder.Api.Endpoints;

public static class DailyExpensesEndpoints
{
    public static IEndpointRouteBuilder MapDailyExpensesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/daily-expenses")
            .WithTags("DailyExpenses")
            .RequireAuthorization();

        group.MapGet("/", async (
            DateOnly? from,
            DateOnly? to,
            Guid? categoryId,
            ClaimsPrincipal principal,
            DailyExpensesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var list = await service.ListAsync(userId, from, to, categoryId, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            DailyExpensesService service,
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
            CreateDailyExpenseRequest request,
            ClaimsPrincipal principal,
            DailyExpensesService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/daily-expenses/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateDailyExpenseRequest request,
            ClaimsPrincipal principal,
            DailyExpensesService service,
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
            DailyExpensesService service,
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
            "validation_error"  => StatusCodes.Status400BadRequest,
            "invalid_account"   => StatusCodes.Status400BadRequest,
            "invalid_category"  => StatusCodes.Status400BadRequest,
            "not_found"         => StatusCodes.Status404NotFound,
            _                   => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
