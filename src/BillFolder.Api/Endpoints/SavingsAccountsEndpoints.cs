using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Savings;
using BillFolder.Application.UseCases.Savings;

namespace BillFolder.Api.Endpoints;

public static class SavingsAccountsEndpoints
{
    public static IEndpointRouteBuilder MapSavingsAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/savings-accounts")
            .WithTags("SavingsAccounts")
            .RequireAuthorization();

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            SavingsAccountsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var list = await service.ListAsync(userId, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            SavingsAccountsService service,
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
            CreateSavingsAccountRequest request,
            ClaimsPrincipal principal,
            SavingsAccountsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/savings-accounts/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateSavingsAccountRequest request,
            ClaimsPrincipal principal,
            SavingsAccountsService service,
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
            SavingsAccountsService service,
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
            "validation_error"               => StatusCodes.Status400BadRequest,
            "invalid_checking_account"       => StatusCodes.Status400BadRequest,
            "checking_already_has_savings"   => StatusCodes.Status409Conflict,
            "not_found"                      => StatusCodes.Status404NotFound,
            _                                => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
