using BillFolder.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Api.Endpoints;

public static class CategoriesEndpoints
{
    public static IEndpointRouteBuilder MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/categories")
            .WithTags("Categories")
            .RequireAuthorization();

        group.MapGet("/", async (
            IApplicationDbContext db,
            CancellationToken ct) =>
        {
            var categories = await db.Categories
                .AsNoTracking()
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CategoryDto(c.Id, c.Key, c.NamePt, c.IsSystem, c.DisplayOrder))
                .ToListAsync(ct);

            return Results.Ok(categories);
        });

        return app;
    }
}

public sealed record CategoryDto(
    Guid Id,
    string Key,
    string NamePt,
    bool IsSystem,
    short DisplayOrder);
