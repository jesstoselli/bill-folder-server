using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.CreditCards;
using BillFolder.Application.UseCases.CreditCards;

namespace BillFolder.Api.Endpoints;

public static class CreditCardAccountsEndpoints
{
    public static IEndpointRouteBuilder MapCreditCardAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/credit-card-accounts")
            .WithTags("CreditCardAccounts")
            .RequireAuthorization();

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            CreditCardAccountsService service,
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
            CreditCardAccountsService service,
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
            CreateCreditCardAccountRequest request,
            ClaimsPrincipal principal,
            CreditCardAccountsService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.CreateAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/v1/credit-card-accounts/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateCreditCardAccountRequest request,
            ClaimsPrincipal principal,
            CreditCardAccountsService service,
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
            CreditCardAccountsService service,
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
            "not_found"        => StatusCodes.Status404NotFound,
            _                  => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
