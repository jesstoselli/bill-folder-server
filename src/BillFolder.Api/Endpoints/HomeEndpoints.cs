using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Home;
using BillFolder.Application.UseCases.Home;

namespace BillFolder.Api.Endpoints;

public static class HomeEndpoints
{
    public static IEndpointRouteBuilder MapHomeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/home")
            .WithTags("Home")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid? cycleId,
            ClaimsPrincipal principal,
            HomeService service,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }
            var result = await service.GetHomeAsync(userId, cycleId, ct);
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
            "no_cycle"  => StatusCodes.Status404NotFound,
            "not_found" => StatusCodes.Status404NotFound,
            _           => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
