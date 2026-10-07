using BillFolder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BillFolder.Api.Tests.Infrastructure;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly FileInfo BaselineSchema = new(Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        "db",
        "schema.sql")));

    private static readonly IReadOnlyDictionary<string, string> TestConfiguration =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ConnectionStrings__Postgres"] = string.Empty,
            ["Jwt__Issuer"] = "billfolder-tests",
            ["Jwt__Audience"] = "billfolder-tests",
            ["Jwt__Key"] = "integration-tests-only-key-with-at-least-32-characters",
            ["Jwt__AccessTokenMinutes"] = "30",
            ["Jwt__RefreshTokenDays"] = "14",
            ["WebAuth__CookieName"] = "bf_refresh",
            ["WebAuth__AllowedOrigins__0"] = "https://app.billfolder.app",
        };

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithResourceMapping(
            BaselineSchema,
            new FileInfo("/docker-entrypoint-initdb.d/001-schema.sql"))
        .WithAutoRemove(true)
        .WithCleanUp(false)
        .Build();

    private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.Ordinal);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _database.StartAsync();

        foreach (var item in TestConfiguration)
        {
            _originalEnvironment[item.Key] = Environment.GetEnvironmentVariable(item.Key);
            var value = item.Key == "ConnectionStrings__Postgres"
                ? _database.GetConnectionString()
                : item.Value;
            Environment.SetEnvironmentVariable(item.Key, value);
        }

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
            await _database.DisposeAsync();
        }
        finally
        {
            foreach (var item in _originalEnvironment)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }
    }
}
