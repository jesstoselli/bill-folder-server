using System.Security.Claims;
using BillFolder.Api.Extensions;
using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Dtos.Auth;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            IApplicationDbContext db,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is null)
            {
                // Token válido mas user não existe mais (deleted) → 401
                return Results.Unauthorized();
            }

            return Results.Ok(new UserDto(user.Id, user.Email, user.DisplayName));
        });

        return app;
    }
}
