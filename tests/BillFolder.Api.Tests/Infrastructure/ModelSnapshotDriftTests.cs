using BillFolder.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BillFolder.Api.Tests.Infrastructure;

public sealed class ModelSnapshotDriftTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public ModelSnapshotDriftTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CurrentModel_ContainsOnlyTheReviewedSnapshotDriftBaseline()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ModelSnapshotDriftGuard.EnsureOnlyAcceptedBaseline(db);
    }
}
