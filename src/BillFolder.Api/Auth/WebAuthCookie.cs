using BillFolder.Application.Dtos.Auth;
using BillFolder.Infrastructure.Auth;

namespace BillFolder.Api.Auth;

public static class WebAuthCookie
{
    private const string CookiePath = "/v1/auth/web";

    public static void Append(
        HttpResponse response,
        AuthResponse authResponse,
        WebAuthOptions options,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(authResponse);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        response.Cookies.Append(
            options.CookieName,
            authResponse.RefreshToken,
            CreateOptions(environment, authResponse.RefreshTokenExpiresAt));
    }

    public static void Delete(
        HttpResponse response,
        WebAuthOptions options,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        response.Cookies.Delete(options.CookieName, CreateOptions(environment));
    }

    private static CookieOptions CreateOptions(
        IHostEnvironment environment,
        DateTime? expiresAt = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = expiresAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)),
        };
    }
}
