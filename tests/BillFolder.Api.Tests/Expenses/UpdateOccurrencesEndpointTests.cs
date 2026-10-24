using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BillFolder.Api.Tests.Infrastructure;
using BillFolder.Application.Dtos.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BillFolder.Api.Tests.Expenses;

/// <summary>
/// POST /v1/expenses/{id}/update-occurrences muda as sessões de UM ciclo só:
/// a recorrência e os outros ciclos ficam como estavam.
/// </summary>
public sealed class UpdateOccurrencesEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery-staple";

    // 2026-12-01 12:00 em São Paulo; dezembro tem 5 terças.
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-12-01T15:00:00Z", CultureInfo.InvariantCulture);

    private readonly HttpClient _client;

    public UpdateOccurrencesEndpointTests(ApiWebApplicationFactory factory)
    {
        _client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now))))
            .CreateClient();
    }

    [Fact]
    public async Task Fewer_sessions_change_only_this_cycle()
    {
        var token = await SignupAsync();
        // Criar dezembro também gera os ciclos seguintes, janeiro incluso.
        await CreateCycleAsync(token, "2026-12-01", "2026-12-31");
        var recurrenceId = await CreateWeeklyRecurrenceAsync(token);
        var december = await ExpenseInAsync(token, "2026-12-01", "2026-12-31");
        december.GetProperty("occurrencesTotal").GetInt32().Should().Be(5);

        var response = await SendAsync(
            token, HttpMethod.Post, $"/v1/expenses/{december.GetProperty("id").GetGuid()}/update-occurrences",
            new { occurrencesTotal = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("occurrencesTotal").GetInt32().Should().Be(3);
        body.RootElement.GetProperty("expectedAmount").GetDecimal().Should().Be(600m);

        var january = await ExpenseInAsync(token, "2027-01-01", "2027-01-31");
        january.GetProperty("occurrencesTotal").GetInt32().Should().Be(4);
        january.GetProperty("expectedAmount").GetDecimal().Should().Be(800m);

        var recurrence = await SendAsync(token, HttpMethod.Get, $"/v1/expense-recurrences/{recurrenceId}");
        using var recurrenceBody = JsonDocument.Parse(await recurrence.Content.ReadAsStringAsync());
        recurrenceBody.RootElement.GetProperty("defaultAmount").GetDecimal().Should().Be(200m);
    }

    [Fact]
    public async Task Fewer_sessions_than_already_paid_is_rejected()
    {
        var token = await SignupAsync();
        await CreateCycleAsync(token, "2026-12-01", "2026-12-31");
        await CreateWeeklyRecurrenceAsync(token);
        var id = (await ExpenseInAsync(token, "2026-12-01", "2026-12-31")).GetProperty("id").GetGuid();
        (await SendAsync(token, HttpMethod.Post, $"/v1/expenses/{id}/pay-occurrence", new { }))
            .EnsureSuccessStatusCode();
        (await SendAsync(token, HttpMethod.Post, $"/v1/expenses/{id}/pay-occurrence", new { }))
            .EnsureSuccessStatusCode();

        var response = await SendAsync(
            token, HttpMethod.Post, $"/v1/expenses/{id}/update-occurrences", new { occurrencesTotal = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("below_paid");
    }

    [Fact]
    public async Task Zero_sessions_is_rejected()
    {
        var token = await SignupAsync();
        await CreateCycleAsync(token, "2026-12-01", "2026-12-31");
        await CreateWeeklyRecurrenceAsync(token);
        var id = (await ExpenseInAsync(token, "2026-12-01", "2026-12-31")).GetProperty("id").GetGuid();

        var response = await SendAsync(
            token, HttpMethod.Post, $"/v1/expenses/{id}/update-occurrences", new { occurrencesTotal = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<string> SignupAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/signup",
            new
            {
                email = $"update-occurrences-{Guid.NewGuid():N}@example.com",
                password = Password,
                displayName = "Update Occurrences Test",
            });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        return auth!.AccessToken;
    }

    private async Task CreateCycleAsync(string token, string startDate, string endDate)
    {
        var response = await SendAsync(
            token, HttpMethod.Post, "/v1/cycles/", new { startDate, endDate, label = startDate });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> CreateWeeklyRecurrenceAsync(string token)
    {
        var categories = await SendAsync(token, HttpMethod.Get, "/v1/categories/");
        using var categoriesBody = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        var categoryId = categoriesBody.RootElement[0].GetProperty("id").GetGuid();

        var response = await SendAsync(
            token,
            HttpMethod.Post,
            "/v1/expense-recurrences/",
            new
            {
                defaultLabel = "Terapia",
                defaultAmount = 200m,
                defaultCategoryId = categoryId,
                frequency = "weekly",
                weekday = 2,
                startDate = "2026-12-01",
            });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> ExpenseInAsync(string token, string from, string to)
    {
        var response = await SendAsync(token, HttpMethod.Get, $"/v1/expenses/?from={from}&to={to}");
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.EnumerateArray().Single().Clone();
    }

    private Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
