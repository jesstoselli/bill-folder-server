using BillFolder.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace BillFolder.Api.Auth;

public sealed class WebOriginFilter(IOptions<WebAuthOptions> options) : IEndpointFilter
{
    private readonly WebAuthOptions _options = options.Value;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var headers = context.HttpContext.Request.Headers;
        if (!headers.TryGetValue("Origin", out var origin))
        {
            return await next(context);
        }

        var isAllowed = origin.Count == 1
            && _options.AllowedOrigins.Contains(origin[0], StringComparer.Ordinal);

        return isAllowed
            ? await next(context)
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}
