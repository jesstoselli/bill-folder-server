using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using BillFolder.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BillFolder.Api.Tests.Auth;

public sealed class WebAuthEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery-staple";
    private const string AllowedOrigin = "https://app.billfolder.app";
    private const string PreviewOrigin = "https://preview.billfolder.app";

    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    public WebAuthEndpointsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    [Fact]
    public async Task WebLogin_SetsSecureHttpOnlyCookie_WithoutReturningRefreshToken()
    {
        var credentials = await CreateUserAsync();

        var response = await _client.PostAsJsonAsync("/v1/auth/web/login", credentials);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("refreshToken");

        var setCookie = response.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        setCookie.Should().ContainAll(
            "bf_refresh=",
            "httponly",
            "secure",
            "samesite=strict",
            "path=/v1/auth/web",
            "expires=");
    }

    [Fact]
    public async Task WebSignup_SetsRefreshCookie_WithoutReturningRefreshToken()
    {
        var email = UniqueEmail();

        var response = await SendWithOriginAsync(
            HttpMethod.Post,
            "/v1/auth/web/signup",
            new { email, password = Password, displayName = "Web User" },
            AllowedOrigin);

        response.EnsureSuccessStatusCode();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("refreshToken");
        RefreshCookieFrom(response).Should().StartWith("bf_refresh=");
    }

    [Fact]
    public async Task WebRefresh_RotatesCookie_AndRejectsReusingTheOldCookie()
    {
        var login = await LoginWebAsync();
        var oldCookie = RefreshCookieFrom(login);

        var firstRefresh = await RefreshWebAsync(oldCookie);

        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        RefreshCookieFrom(firstRefresh).Should().NotBe(oldCookie);

        var replay = await RefreshWebAsync(oldCookie);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertErrorAsync(replay, "invalid_refresh_token");
    }

    [Fact]
    public async Task WebRefresh_ConcurrentReplay_AllowsExactlyOneRotation()
    {
        var login = await LoginWebAsync();
        var oldCookie = RefreshCookieFrom(login);
        var barrier = _factory.Services.GetRequiredService<RefreshTokenLookupBarrier>();
        barrier.Enable(participants: 2);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var attempts = new[]
        {
            RefreshWebAsync(oldCookie, timeout.Token),
            RefreshWebAsync(oldCookie, timeout.Token),
        };

        var responses = await Task.WhenAll(attempts);

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);

        var winnerCookie = RefreshCookieFrom(responses.Single(
            response => response.StatusCode == HttpStatusCode.OK));
        var winnerSuccessor = await RefreshWebAsync(winnerCookie, timeout.Token);
        winnerSuccessor.StatusCode.Should().Be(HttpStatusCode.OK);

        var oldTokenReplay = await RefreshWebAsync(oldCookie, timeout.Token);
        oldTokenReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WebLogout_WithRotatedPredecessor_RevokesActiveFamilySuccessor()
    {
        var login = await LoginWebAsync();
        var predecessorCookie = RefreshCookieFrom(login);
        var rotation = await RefreshWebAsync(predecessorCookie);
        var successorCookie = RefreshCookieFrom(rotation);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/web/logout");
        logoutRequest.Headers.Add("Cookie", predecessorCookie);
        var logout = await _client.SendAsync(logoutRequest);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var refresh = await RefreshWebAsync(successorCookie);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertErrorAsync(refresh, "invalid_refresh_token");
    }

    [Fact]
    public async Task WebLogout_ConcurrentWithRefresh_RevokesTheSuccessorCreatedByTheRefresh()
    {
        var login = await LoginWebAsync();
        var predecessorCookie = RefreshCookieFrom(login);
        var barrier = _factory.Services.GetRequiredService<RefreshTokenInsertBarrier>();
        barrier.Enable();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var rotationTask = RefreshWebAsync(predecessorCookie, timeout.Token);
        await barrier.WaitUntilInsertEnteredAsync(timeout.Token);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/web/logout");
        logoutRequest.Headers.Add("Cookie", predecessorCookie);
        var logoutTask = _client.SendAsync(logoutRequest, timeout.Token);

        await barrier.WaitUntilContenderEnteredAsync(timeout.Token);
        barrier.ReleaseInsert();

        var rotation = await rotationTask;
        var logout = await logoutTask;
        rotation.StatusCode.Should().Be(HttpStatusCode.OK);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var successorCookie = RefreshCookieFrom(rotation);
        var refresh = await RefreshWebAsync(successorCookie, timeout.Token);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertErrorAsync(refresh, "invalid_refresh_token");
    }

    [Fact]
    public async Task WebRefresh_WithoutCookie_ReturnsExistingInvalidRefreshTokenShape()
    {
        var response = await _client.PostAsync("/v1/auth/web/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertErrorAsync(response, "invalid_refresh_token");
    }

    [Fact]
    public async Task WebLogout_RevokesPresentCookie_ThenDeletesItWithMatchingPolicy()
    {
        var login = await LoginWebAsync();
        var cookie = RefreshCookieFrom(login);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/web/logout");
        request.Headers.Add("Cookie", cookie);
        var logout = await _client.SendAsync(request);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deletedCookie = logout.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        deletedCookie.Should().ContainAll(
            "bf_refresh=",
            "httponly",
            "secure",
            "samesite=strict",
            "path=/v1/auth/web");

        var refresh = await RefreshWebAsync(cookie);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WebLogout_WithoutCookie_IsIdempotent_AndDeletesCookie()
    {
        var response = await _client.PostAsync("/v1/auth/web/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Single().Should().Contain("bf_refresh=");
    }

    [Fact]
    public async Task WebEndpoint_AllowsMissingOrigin_ForNonBrowserClients()
    {
        var credentials = await CreateUserAsync();

        var response = await _client.PostAsJsonAsync("/v1/auth/web/login", credentials);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("https://evil.example")]
    [InlineData("https://APP.billfolder.app")]
    public async Task WebEndpoint_RejectsAnySuppliedOriginWithoutExactOrdinalMatch(string origin)
    {
        var response = await SendWithOriginAsync(
            HttpMethod.Post,
            "/v1/auth/web/refresh",
            body: null,
            origin);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WebEndpoint_AllowsExactConfiguredOrigin()
    {
        var login = await LoginWebAsync();
        var cookie = RefreshCookieFrom(login);

        var response = await SendWithOriginAsync(
            HttpMethod.Post,
            "/v1/auth/web/refresh",
            body: null,
            AllowedOrigin,
            cookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be(AllowedOrigin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Single().Should().Be("true");
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("null")]
    public async Task Cors_RejectsUnknownPreflightOriginWithForbidden(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/web/refresh");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_RejectsUnknownPreflightOriginForCaseInsensitiveRouteAlias()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/V1/AUTH/WEB/refresh");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_RejectsPreflightWithMultipleOriginValues()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/web/refresh");
        request.Headers.TryAddWithoutValidation(
            "Origin",
            ["https://app.billfolder.app", "https://evil.example"]);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_DoesNotApplyWebAuthGuardOutsideSegmentBoundary()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/webfoo");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOriginWithCredentials()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/web/refresh");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be(AllowedOrigin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Single().Should().Be("true");
    }

    [Fact]
    public async Task Cors_AllowsOnlyTheExactAuthenticatedPreviewOrigin()
    {
        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/web/refresh");
        allowed.Headers.Add("Origin", PreviewOrigin);
        allowed.Headers.Add("Access-Control-Request-Method", "POST");

        var allowedResponse = await _client.SendAsync(allowed);

        allowedResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single()
            .Should().Be(PreviewOrigin);

        using var rejected = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/web/refresh");
        rejected.Headers.Add("Origin", "https://branch.billfolder.pages.dev");
        rejected.Headers.Add("Access-Control-Request-Method", "POST");

        var rejectedResponse = await _client.SendAsync(rejected);

        rejectedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        rejectedResponse.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task AuthResponse_UsesConfiguredAccessTokenExpiration()
    {
        await using var factory = ApiWebApplicationFactory.Create(
            "Production",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Jwt:AccessTokenMinutes"] = "7",
            });
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        var response = await client.PostAsJsonAsync(
            "/v1/auth/signup",
            new { email = UniqueEmail(), password = Password, displayName = "Expiry User" });

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("accessToken").GetString();
        var responseExpiry = json.GetProperty("accessTokenExpiresAt").GetDateTime();
        var jwtExpiry = new JwtSecurityTokenHandler().ReadJwtToken(accessToken).ValidTo;

        responseExpiry.Should().Be(jwtExpiry);
    }

    [Fact]
    public async Task MobileSignupLoginAndRefresh_StillReturnRefreshToken()
    {
        var email = UniqueEmail();
        var signup = await _client.PostAsJsonAsync(
            "/v1/auth/signup",
            new { email, password = Password, displayName = "Mobile User" });
        signup.EnsureSuccessStatusCode();
        await AssertRefreshTokenAsync(signup);

        var login = await _client.PostAsJsonAsync("/v1/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var refreshToken = await AssertRefreshTokenAsync(login);

        var refresh = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken });
        refresh.EnsureSuccessStatusCode();
        await AssertRefreshTokenAsync(refresh);
    }

    private async Task<HttpResponseMessage> LoginWebAsync()
    {
        var credentials = await CreateUserAsync();
        return await _client.PostAsJsonAsync("/v1/auth/web/login", credentials);
    }

    private async Task<HttpResponseMessage> RefreshWebAsync(
        string cookie,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/web/refresh");
        request.Headers.Add("Cookie", cookie);
        return await _client.SendAsync(request, cancellationToken);
    }

    private static string RefreshCookieFrom(HttpResponseMessage response)
    {
        return response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("bf_refresh=", StringComparison.Ordinal))
            .Split(';', 2, StringSplitOptions.TrimEntries)[0];
    }

    private async Task<object> CreateUserAsync()
    {
        var email = UniqueEmail();
        var signup = await _client.PostAsJsonAsync(
            "/v1/auth/signup",
            new { email, password = Password, displayName = "Existing User" });
        signup.EnsureSuccessStatusCode();
        return new { email, password = Password };
    }

    private async Task<HttpResponseMessage> SendWithOriginAsync(
        HttpMethod method,
        string path,
        object? body,
        string origin,
        string? cookie = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", origin);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string expectedError)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be(expectedError);
        json.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<string> AssertRefreshTokenAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("refreshToken", out var token).Should().BeTrue();
        token.GetString().Should().NotBeNullOrWhiteSpace();
        return token.GetString()!;
    }

    private static string UniqueEmail() => $"web-{Guid.NewGuid():N}@example.com";
}
