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

namespace BillFolder.Api.Tests.Recurrences;

/// <summary>
/// A monthly expense recurrence materializes from its start date, not from
/// the day it was created: a start date in the past means the user wants the
/// bills that already fell due since then.
/// </summary>
public sealed class MonthlyRecurrenceStartDateTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery-staple";

    // 2026-10-14 12:00 in São Paulo.
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-14T15:00:00Z", CultureInfo.InvariantCulture);

    private readonly HttpClient _client;

    public MonthlyRecurrenceStartDateTests(ApiWebApplicationFactory factory)
    {
        _client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now))))
            .CreateClient();
    }

    [Fact]
    public async Task StartDateInThePast_CreatesTheBillAlreadyDueInTheCurrentCycle()
    {
        var token = await SignupAsync();
        await CreateCycleAsync(token, "2026-10-01", "2026-10-31");

        await CreateMonthlyRecurrenceAsync(token, startDate: "2026-10-01", dueDay: 5);

        (await DueDatesAsync(token, "2026-10-01", "2026-10-31"))
            .Should().Equal("2026-10-05");
    }

    [Fact]
    public async Task StartDateToday_DoesNotCreateABillThatFellDueEarlierThisCycle()
    {
        var token = await SignupAsync();
        await CreateCycleAsync(token, "2026-10-01", "2026-10-31");

        await CreateMonthlyRecurrenceAsync(token, startDate: "2026-10-14", dueDay: 5);

        (await DueDatesAsync(token, "2026-10-01", "2026-10-31")).Should().BeEmpty();
    }

    private async Task<string> SignupAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/signup",
            new
            {
                email = $"monthly-recurrence-{Guid.NewGuid():N}@example.com",
                password = Password,
                displayName = "Monthly Recurrence Test",
            });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        return auth!.AccessToken;
    }

    private async Task CreateCycleAsync(string token, string startDate, string endDate)
    {
        var response = await SendAsync(
            token,
            HttpMethod.Post,
            "/v1/cycles/",
            new { startDate, endDate, label = "Outubro" });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private async Task CreateMonthlyRecurrenceAsync(string token, string startDate, int dueDay)
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
                defaultLabel = "Condomínio",
                defaultAmount = 800m,
                defaultCategoryId = categoryId,
                frequency = "monthly",
                dueDay,
                startDate,
            });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private async Task<string[]> DueDatesAsync(string token, string from, string to)
    {
        var response = await SendAsync(token, HttpMethod.Get, $"/v1/expenses/?from={from}&to={to}");
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.EnumerateArray()
            .Select(expense => expense.GetProperty("dueDate").GetString()!)
            .ToArray();
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
