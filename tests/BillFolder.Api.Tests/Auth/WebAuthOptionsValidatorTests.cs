using BillFolder.Api.Auth;
using BillFolder.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BillFolder.Api.Tests.Auth;

public sealed class WebAuthOptionsValidatorTests
{
    public static TheoryData<string, WebAuthOptions> InvalidOptions => new()
    {
        {
            Environments.Production,
            new WebAuthOptions
            {
                CookieName = " ",
                AllowedOrigins = ["https://app.billfolder.app"],
            }
        },
        {
            Environments.Production,
            new WebAuthOptions
            {
                CookieName = "bf/refresh",
                AllowedOrigins = ["https://app.billfolder.app"],
            }
        },
        {
            Environments.Production,
            new WebAuthOptions { CookieName = "bf_refresh", AllowedOrigins = [] }
        },
        {
            Environments.Production,
            new WebAuthOptions
            {
                CookieName = "bf_refresh",
                AllowedOrigins =
                [
                    "https://app.billfolder.app",
                    "https://app.billfolder.app",
                ],
            }
        },
        {
            Environments.Production,
            new WebAuthOptions
            {
                CookieName = "bf_refresh",
                AllowedOrigins = ["https://app.billfolder.app/path"],
            }
        },
        {
            Environments.Production,
            new WebAuthOptions
            {
                CookieName = "bf_refresh",
                AllowedOrigins = ["http://app.billfolder.app"],
            }
        },
        {
            Environments.Development,
            new WebAuthOptions
            {
                CookieName = "bf_refresh",
                AllowedOrigins = ["http://app.billfolder.app"],
            }
        },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Validate_RejectsUnsafeConfiguration(
        string environmentName,
        WebAuthOptions options)
    {
        var validator = new WebAuthOptionsValidator(new TestHostEnvironment(environmentName));

        var result = validator.Validate(name: null, options);

        result.Failed.Should().BeTrue();
    }

    [Theory]
    [InlineData("Production", "https://app.billfolder.app")]
    [InlineData("Development", "http://localhost:4200")]
    [InlineData("Development", "http://127.0.0.1:4201")]
    public void Validate_AcceptsEnvironmentAppropriateOrigins(
        string environmentName,
        string origin)
    {
        var validator = new WebAuthOptionsValidator(new TestHostEnvironment(environmentName));
        var options = new WebAuthOptions
        {
            CookieName = "bf_refresh",
            AllowedOrigins = [origin],
        };

        var result = validator.Validate(name: null, options);

        result.Succeeded.Should().BeTrue();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "BillFolder.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
