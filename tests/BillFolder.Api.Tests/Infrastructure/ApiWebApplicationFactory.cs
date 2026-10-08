using BillFolder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    private static readonly FileInfo RefreshFamilyMigration = new(Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        "db",
        "migrations",
        "20261008120000_AddRefreshTokenFamilies.sql")));

    private static readonly IReadOnlyDictionary<string, string?> TestConfiguration =
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Jwt:Issuer"] = "billfolder-tests",
            ["Jwt:Audience"] = "billfolder-tests",
            ["Jwt:Key"] = "integration-tests-only-key-with-at-least-32-characters",
            ["Jwt:AccessTokenMinutes"] = "30",
            ["Jwt:RefreshTokenDays"] = "14",
            ["WebAuth:CookieName"] = "bf_refresh",
            ["WebAuth:AllowedOrigins:0"] = "https://app.billfolder.app",
            ["BillFolderTests:FactoryConfigurationMustRemainProcessLocal_9D744F"] = "enabled",
        };

    private readonly string _environment;
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithResourceMapping(
            BaselineSchema,
            new FileInfo("/docker-entrypoint-initdb.d/001-schema.sql"))
        .WithAutoRemove(true)
        .WithCleanUp(false)
        .WithLabel("com.billfolder.test-suite", "api-integration")
        .Build();

    public ApiWebApplicationFactory()
        : this("Production", new Dictionary<string, string?>(StringComparer.Ordinal))
    {
    }

    private ApiWebApplicationFactory(
        string environment,
        IReadOnlyDictionary<string, string?> configurationOverrides)
    {
        _environment = environment;
        _configurationOverrides = configurationOverrides;
    }

    public static ApiWebApplicationFactory Create(
        string environment,
        IReadOnlyDictionary<string, string?> configurationOverrides)
    {
        return new ApiWebApplicationFactory(environment, configurationOverrides);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<RefreshTokenLookupBarrier>();
            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                options.AddInterceptors(serviceProvider.GetRequiredService<RefreshTokenLookupBarrier>());
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>(TestConfiguration, StringComparer.Ordinal)
            {
                ["ConnectionStrings:Postgres"] = _database.GetConnectionString(),
            };

            foreach (var item in _configurationOverrides)
            {
                settings[item.Key] = item.Value;
            }

            configuration.AddInMemoryCollection(settings);
        });

        return base.CreateHost(builder);
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ModelSnapshotDriftGuard.EnsureOnlyAcceptedBaseline(db);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(RefreshFamilyMigration.FullName));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await _database.DisposeAsync();
        }
    }
}
