using BillFolder.Api.Auth;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Auth;
using BillFolder.Application.UseCases.Auth;
using BillFolder.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace BillFolder.Api.Endpoints;

public static class WebAuthEndpoints
{
    public static IEndpointRouteBuilder MapWebAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth/web")
            .WithTags("Web Auth")
            .AddEndpointFilter<WebOriginFilter>();

        group.MapPost("/signup", async (
            SignupRequest request,
            AuthService auth,
            HttpContext httpContext,
            IOptions<WebAuthOptions> options,
            IHostEnvironment environment,
            CancellationToken ct) =>
        {
            var result = await auth.SignupAsync(request, ct);
            return ToWebAuthResult(result, httpContext.Response, options.Value, environment);
        });

        group.MapPost("/login", async (
            LoginRequest request,
            AuthService auth,
            HttpContext httpContext,
            IOptions<WebAuthOptions> options,
            IHostEnvironment environment,
            CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(request, ct);
            return ToWebAuthResult(result, httpContext.Response, options.Value, environment);
        });

        group.MapPost("/refresh", async (
            AuthService auth,
            HttpContext httpContext,
            IOptions<WebAuthOptions> options,
            IHostEnvironment environment,
            CancellationToken ct) =>
        {
            var webOptions = options.Value;
            if (!httpContext.Request.Cookies.TryGetValue(webOptions.CookieName, out var refreshToken)
                || string.IsNullOrWhiteSpace(refreshToken))
            {
                return InvalidRefreshToken();
            }

            var result = await auth.RefreshAsync(new RefreshTokenRequest(refreshToken), ct);
            return ToWebAuthResult(result, httpContext.Response, webOptions, environment);
        });

        group.MapPost("/logout", async (
            AuthService auth,
            HttpContext httpContext,
            IOptions<WebAuthOptions> options,
            IHostEnvironment environment,
            CancellationToken ct) =>
        {
            var webOptions = options.Value;
            if (httpContext.Request.Cookies.TryGetValue(webOptions.CookieName, out var refreshToken)
                && !string.IsNullOrWhiteSpace(refreshToken))
            {
                var result = await auth.LogoutAsync(new LogoutRequest(refreshToken), ct);
                if (!result.IsSuccess)
                {
                    return ToErrorResult(result);
                }
            }

            WebAuthCookie.Delete(httpContext.Response, webOptions, environment);
            return Results.NoContent();
        });

        return app;
    }

    private static IResult ToWebAuthResult(
        OperationResult<AuthResponse> result,
        HttpResponse response,
        WebAuthOptions options,
        IHostEnvironment environment)
    {
        if (!result.IsSuccess || result.Value is null)
        {
            return ToErrorResult(result);
        }

        WebAuthCookie.Append(response, result.Value, options, environment);
        return Results.Ok(new WebAuthResponse(
            result.Value.AccessToken,
            result.Value.AccessTokenExpiresAt,
            result.Value.User));
    }

    private static IResult InvalidRefreshToken()
    {
        return Results.Json(
            new
            {
                error = "invalid_refresh_token",
                message = "Refresh token inválido, revogado ou expirado.",
            },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static IResult ToErrorResult<T>(OperationResult<T> result)
    {
        var status = result.ErrorCode switch
        {
            "validation_error" => StatusCodes.Status400BadRequest,
            "email_already_registered" => StatusCodes.Status409Conflict,
            "invalid_credentials" => StatusCodes.Status401Unauthorized,
            "invalid_refresh_token" => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Json(
            new { error = result.ErrorCode, message = result.ErrorMessage },
            statusCode: status);
    }
}
