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

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            AuthService auth,
            CancellationToken ct) =>
        {
            var result = await auth.RefreshAsync(request, ct);
            return ToHttpResult(result);
        });

        group.MapPost("/logout", async (
            LogoutRequest request,
            AuthService auth,
            CancellationToken ct) =>
        {
            var result = await auth.LogoutAsync(request, ct);
            // 204 No Content em sucesso (idempotente)
            return result.IsSuccess
                ? Results.NoContent()
                : ToHttpResult(result);
        });

        // Inicia fluxo de reset de senha. Sempre 200 (proteção contra
        // enumeration). DevCode no body é populado apenas em dev sem
        // IEmailSender configurado — facilita testar via curl.
        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            AuthService auth,
            CancellationToken ct) =>
        {
            var result = await auth.ForgotPasswordAsync(request, ct);
            return ToHttpResult(result);
        });

        // Conclui o reset. Valida código + email; em sucesso troca senha
        // e revoga refresh tokens ativos do user.
        group.MapPost("/reset-password", async (
            ResetPasswordRequest request,
            AuthService auth,
            CancellationToken ct) =>
        {
            var result = await auth.ResetPasswordAsync(request, ct);
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
            "validation_error"          => StatusCodes.Status400BadRequest,
            "email_already_registered"  => StatusCodes.Status409Conflict,
            "invalid_credentials"       => StatusCodes.Status401Unauthorized,
            "invalid_refresh_token"     => StatusCodes.Status401Unauthorized,
            "invalid_reset_code"        => StatusCodes.Status400BadRequest,
            _                           => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
