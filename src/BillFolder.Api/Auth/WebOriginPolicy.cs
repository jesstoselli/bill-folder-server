using BillFolder.Infrastructure.Auth;

namespace BillFolder.Api.Auth;

public static class WebOriginPolicy
{
    public static bool IsAllowed(HttpRequest request, WebAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        if (!request.Headers.TryGetValue("Origin", out var origin))
        {
            return true;
        }

        return origin.Count == 1
            && options.AllowedOrigins.Contains(origin[0], StringComparer.Ordinal);
    }
}
