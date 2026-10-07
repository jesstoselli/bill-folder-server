using BillFolder.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace BillFolder.Api.Auth;

public sealed class WebAuthOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<WebAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, WebAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!IsValidCookieName(options.CookieName))
        {
            failures.Add("WebAuth:CookieName must be a non-empty HTTP token.");
        }

        ValidateOrigins(options.AllowedOrigins, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private void ValidateOrigins(string[]? origins, List<string> failures)
    {
        if (origins is null || origins.Length == 0)
        {
            failures.Add("WebAuth:AllowedOrigins must contain at least one origin.");
            return;
        }

        var uniqueOrigins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var origin in origins)
        {
            if (!uniqueOrigins.Add(origin))
            {
                failures.Add("WebAuth:AllowedOrigins must not contain duplicates.");
                continue;
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || !string.Equals(
                    origin,
                    uri.GetLeftPart(UriPartial.Authority),
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                failures.Add(
                    "WebAuth:AllowedOrigins entries must be absolute origins without path, query, fragment, or user info.");
                continue;
            }

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                continue;
            }

            var isDevelopmentLoopbackHttp = environment.IsDevelopment()
                && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && uri.IsLoopback;
            if (!isDevelopmentLoopbackHttp)
            {
                failures.Add(
                    "WebAuth:AllowedOrigins entries must use HTTPS, except loopback HTTP origins in Development.");
            }
        }
    }

    private static bool IsValidCookieName(string? cookieName)
    {
        return !string.IsNullOrWhiteSpace(cookieName)
            && cookieName.All(IsHttpTokenCharacter);
    }

    private static bool IsHttpTokenCharacter(char value)
    {
        return value is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '!'
            or '#'
            or '$'
            or '%'
            or '&'
            or '\''
            or '*'
            or '+'
            or '-'
            or '.'
            or '^'
            or '_'
            or '`'
            or '|'
            or '~';
    }
}
