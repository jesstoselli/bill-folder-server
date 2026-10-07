using System.Net;
using System.Net.Http.Json;
using BillFolder.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BillFolder.Api.Tests.Auth;

public sealed class DevelopmentWebAuthEndpointsTests : IAsyncLifetime
{
    private const string Password = "correct-horse-battery-staple";
    private const string LocalhostOrigin = "http://localhost:4200";
    private const string AdditionalLoopbackOrigin = "http://127.0.0.1:4201";

    private readonly ApiWebApplicationFactory _factory = ApiWebApplicationFactory.Create(
        "Development",
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["WebAuth:AllowedOrigins:0"] = LocalhostOrigin,
            ["WebAuth:AllowedOrigins:1"] = AdditionalLoopbackOrigin,
        });

    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    public async Task DisposeAsync()
    {
        try
        {
            _client?.Dispose();
        }
        finally
        {
            await ((IAsyncLifetime)_factory).DisposeAsync();
        }
    }

    [Fact]
    public async Task DevelopmentHost_AllowsConfiguredLoopbackOrigins_AndOmitsSecureCookie()
    {
        var email = $"web-dev-{Guid.NewGuid():N}@example.com";
        var credentials = new { email, password = Password };
        var signup = await Client.PostAsJsonAsync(
            "/v1/auth/signup",
            new { email, password = Password, displayName = "Development User" });
        signup.EnsureSuccessStatusCode();

        var localhostLogin = await LoginWithOriginAsync(credentials, LocalhostOrigin);
        localhostLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        var configuredProviderLogin = await LoginWithOriginAsync(
            credentials,
            AdditionalLoopbackOrigin);
        configuredProviderLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        var setCookie = localhostLogin.Headers.GetValues("Set-Cookie").Single();
        setCookie.Should().Contain("bf_refresh=");
        setCookie.ToLowerInvariant().Should().NotContain("secure");
    }

    private async Task<HttpResponseMessage> LoginWithOriginAsync(object credentials, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/web/login")
        {
            Content = JsonContent.Create(credentials),
        };
        request.Headers.Add("Origin", origin);
        return await Client.SendAsync(request);
    }

    private HttpClient Client => _client
        ?? throw new InvalidOperationException("The test host has not been initialized.");
}
