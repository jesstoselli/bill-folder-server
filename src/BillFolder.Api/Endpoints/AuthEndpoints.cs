using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Auth;
using BillFolder.Application.UseCases.Auth;

namespace BillFolder.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth").WithTags("Auth");

        group.MapPost("/signup", async (
            SignupRequest request,
            AuthService auth,
            CancellationToken ct) =>
        {
            var result = await auth.SignupAsync(request, ct);
            return ToHttpResult(result);
        });

        group.MapPost("/login", async (
            LoginRequest request,
            AuthService auth,
            CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(request, ct);
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
            "validation_error"          => StatusCodes.Status400BadRequest,
            "email_already_registered"  => StatusCodes.Status409Conflict,
            "invalid_credentials"       => StatusCodes.Status401Unauthorized,
            _                           => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
