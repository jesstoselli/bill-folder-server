using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BillFolder.Api.Tests.Infrastructure;
using BillFolder.Application.Dtos.Auth;
using FluentAssertions;

namespace BillFolder.Api.Tests.Incomes;

public sealed class IncomeSourceEndDateTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private readonly HttpClient _client;

    public IncomeSourceEndDateTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Patch_ClearEndDate_RemovesTheEndDate()
    {
        var accessToken = await SignupAsync();
        var sourceId = await CreateSourceAsync(accessToken, endDate: "2026-12-31");

        var response = await PatchAsync(accessToken, sourceId, new { clearEndDate = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EndDateOfAsync(response)).ValueKind.Should().Be(JsonValueKind.Null);
        (await EndDateOfAsync(await GetAsync(accessToken, sourceId)))
            .ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Patch_WithoutEndDate_KeepsTheEndDate()
    {
        var accessToken = await SignupAsync();
        var sourceId = await CreateSourceAsync(accessToken, endDate: "2026-12-31");

        var response = await PatchAsync(accessToken, sourceId, new { origin = "Salário novo" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EndDateOfAsync(response)).GetString().Should().Be("2026-12-31");
    }

    [Fact]
    public async Task Patch_ClearEndDateTogetherWithEndDate_IsRejected()
    {
        var accessToken = await SignupAsync();
        var sourceId = await CreateSourceAsync(accessToken, endDate: "2026-12-31");

        var response = await PatchAsync(
            accessToken,
            sourceId,
            new { clearEndDate = true, endDate = "2027-01-31" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await EndDateOfAsync(await GetAsync(accessToken, sourceId)))
            .GetString().Should().Be("2026-12-31");
    }

    [Fact]
    public async Task Patch_ClearEndDate_SkipsTheEndBeforeStartCheck()
    {
        // The stored end date would be before the new start date; clearing it
        // in the same request must make the update valid.
        var accessToken = await SignupAsync();
        var sourceId = await CreateSourceAsync(accessToken, endDate: "2026-11-30");

        var response = await PatchAsync(
            accessToken,
            sourceId,
            new { startDate = "2027-01-01", clearEndDate = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EndDateOfAsync(response)).ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task<string> SignupAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/signup",
            new
            {
                email = $"income-source-end-{Guid.NewGuid():N}@example.com",
                password = Password,
                displayName = "Income Source End Date Test",
            });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        return auth!.AccessToken;
    }

    private async Task<Guid> CreateSourceAsync(string accessToken, string endDate)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/income-sources/")
        {
            Content = JsonContent.Create(new
            {
                origin = "Salário",
                originType = "work",
                defaultAmount = 5000m,
                expectedDay = 5,
                startDate = "2026-10-01",
                endDate,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PatchAsync(string accessToken, Guid id, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/income-sources/{id}")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAsync(string accessToken, Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/income-sources/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private static async Task<JsonElement> EndDateOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("endDate").Clone();
    }
}
