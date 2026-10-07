using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BillFolder.Api.Tests.Infrastructure;

public sealed class ApiWebApplicationFactoryTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string EnvironmentKey =
        "BillFolderTests__FactoryConfigurationMustRemainProcessLocal_9D744F";

    private readonly ApiWebApplicationFactory _factory;

    public ApiWebApplicationFactoryTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void FactoryConfiguration_DoesNotMutateProcessEnvironment()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        configuration["BillFolderTests:FactoryConfigurationMustRemainProcessLocal_9D744F"]
            .Should().Be("enabled");
        Environment.GetEnvironmentVariable(EnvironmentKey).Should().BeNull();
    }

    [Fact]
    public async Task Startup_RejectsUnsafeWebAuthOptions()
    {
        var factory = ApiWebApplicationFactory.Create(
            "Production",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["WebAuth:AllowedOrigins:0"] = "http://app.billfolder.app",
            });

        try
        {
            Func<Task> action = factory.InitializeAsync;

            await action.Should().ThrowAsync<OptionsValidationException>();
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }
}
