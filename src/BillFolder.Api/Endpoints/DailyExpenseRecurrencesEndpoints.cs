using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Recurrences;
using BillFolder.Application.UseCases.Recurrences;

namespace BillFolder.Api.Endpoints;

public static class DailyExpenseRecurrencesEndpoints
{
    public static IEndpointRouteBuilder MapDailyExpenseRecurrencesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/daily-expense-recurrences")
            .WithTags("DailyExpenseRecurrences")
            .RequireAuthorization();

        group.MapGet("/", async (
            bool? activeOnly, ClaimsPrincipal principal,
            DailyExpenseRecurrencesService service, CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            return Results.Ok(await service.ListAsync(userId, activeOnly, ct));
        });

        group.MapGet("/{id:guid}", async (
            Guid id, ClaimsPrincipal principal,
            DailyExpenseRecurrencesService service, CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            return ToHttpResult(await service.GetAsync(userId, id, ct));
        });

        group.MapPost("/", async (
            CreateDailyExpenseRecurrenceRequest request, ClaimsPrincipal principal,
            DailyExpenseRecurrencesService service, CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/daily-expense-recurrences/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id, UpdateDailyExpenseRecurrenceRequest request, ClaimsPrincipal principal,
            DailyExpenseRecurrencesService service, CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            return ToHttpResult(await service.UpdateAsync(userId, id, request, ct));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id, ClaimsPrincipal principal,
            DailyExpenseRecurrencesService service, CancellationToken ct) =>
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
            "validation_error" => StatusCodes.Status400BadRequest,
            "invalid_category" => StatusCodes.Status400BadRequest,
            "invalid_account"  => StatusCodes.Status400BadRequest,
            "not_found"        => StatusCodes.Status404NotFound,
            _                  => StatusCodes.Status400BadRequest,
        };
        return Results.Json(new { error = result.ErrorCode, message = result.ErrorMessage }, statusCode: status);
    }
}
