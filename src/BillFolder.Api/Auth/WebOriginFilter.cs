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
        return WebOriginPolicy.IsAllowed(context.HttpContext.Request, _options)
            ? await next(context)
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}
