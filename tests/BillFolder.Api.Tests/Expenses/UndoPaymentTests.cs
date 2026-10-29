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
/// Desfazer um pagamento confirmado por engano: a despesa (ou a fatura) volta
/// a ficar em aberto e perde data, valor e conta do pagamento.
/// </summary>
public sealed class UndoPaymentTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-29T15:00:00Z", CultureInfo.InvariantCulture);

    private readonly HttpClient _client;

    public UndoPaymentTests(ApiWebApplicationFactory factory)
    {
        _client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now))))
            .CreateClient();
    }

    [Fact]
    public async Task Undo_puts_a_paid_expense_back_to_pending_without_payment_data()
    {
        var token = await SignupAsync();
        var accountId = await CreateAccountAsync(token);
        var id = await CreateExpenseAsync(token, dueDate: "2026-11-10");
        (await SendAsync(token, HttpMethod.Patch, $"/v1/expenses/{id}",
            new { status = "paid", actualAmount = 180m, paidFromAccountId = accountId }))
            .EnsureSuccessStatusCode();

        var response = await SendAsync(token, HttpMethod.Post, $"/v1/expenses/{id}/unpay");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("pending");
        body.RootElement.GetProperty("paidDate").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("actualAmount").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("paidFromAccountId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Undo_of_a_past_due_expense_shows_it_overdue()
    {
        var token = await SignupAsync();
        var id = await CreateExpenseAsync(token, dueDate: "2026-10-05");
        (await SendAsync(token, HttpMethod.Patch, $"/v1/expenses/{id}", new { status = "paid" }))
            .EnsureSuccessStatusCode();

        var response = await SendAsync(token, HttpMethod.Post, $"/v1/expenses/{id}/unpay");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("overdue");
    }

    [Fact]
    public async Task Undo_of_an_unpaid_expense_is_rejected()
    {
        var token = await SignupAsync();
        var id = await CreateExpenseAsync(token, dueDate: "2026-11-10");

        var response = await SendAsync(token, HttpMethod.Post, $"/v1/expenses/{id}/unpay");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("not_paid");
    }

    [Fact]
    public async Task Undo_puts_a_paid_statement_back_to_closed_and_payable()
    {
        var token = await SignupAsync();
        var statementId = await CreateClosedStatementAsync(token);
        (await PayStatementAsync(token, statementId)).EnsureSuccessStatusCode();

        var response = await SendAsync(token, HttpMethod.Post, $"/v1/card-statements/{statementId}/unpay");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("closed");
        body.RootElement.GetProperty("paidDate").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("actualAmount").ValueKind.Should().Be(JsonValueKind.Null);
        (await PayStatementAsync(token, statementId)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Undo_of_an_unpaid_statement_is_rejected()
    {
        var token = await SignupAsync();
        var statementId = await CreateClosedStatementAsync(token);

        var response = await SendAsync(token, HttpMethod.Post, $"/v1/card-statements/{statementId}/unpay");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("statement_not_paid");
    }

    private async Task<string> SignupAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/signup",
            new
            {
                email = $"undo-payment-{Guid.NewGuid():N}@example.com",
                password = Password,
                displayName = "Undo Payment Test",
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<Guid> CategoryAsync(string token)
    {
        var categories = await SendAsync(token, HttpMethod.Get, "/v1/categories/");
        using var body = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        return body.RootElement[0].GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateAccountAsync(string token)
    {
        var response = await SendAsync(
            token,
            HttpMethod.Post,
            "/v1/checking-accounts/",
            new { bankName = "Banco", branch = "0001", accountNumber = "12345-6", initialBalance = 1000m, isPrimary = true });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateExpenseAsync(string token, string dueDate)
    {
        var response = await SendAsync(
            token,
            HttpMethod.Post,
            "/v1/expenses/",
            new { dueDate, label = "Luz", expectedAmount = 180m, categoryId = await CategoryAsync(token) });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateClosedStatementAsync(string token)
    {
        var card = await SendAsync(
            token, HttpMethod.Post, "/v1/credit-card-accounts/", new { name = "Nubank", closingDay = 10, dueDay = 17 });
        using var cardBody = JsonDocument.Parse(await card.Content.ReadAsStringAsync());
        var cardId = cardBody.RootElement.GetProperty("id").GetGuid();

        (await SendAsync(
            token,
            HttpMethod.Post,
            "/v1/card-entries/",
            new
            {
                cardId,
                purchaseDate = "2026-09-15",
                label = "Tênis",
                totalAmount = 300m,
                installmentsCount = 1,
                categoryId = await CategoryAsync(token),
            })).EnsureSuccessStatusCode();

        var statements = await SendAsync(token, HttpMethod.Get, $"/v1/card-statements/?cardId={cardId}");
        using var statementsBody = JsonDocument.Parse(await statements.Content.ReadAsStringAsync());
        return statementsBody.RootElement[0].GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PayStatementAsync(string token, Guid statementId) =>
        SendAsync(
            token,
            HttpMethod.Post,
            $"/v1/card-statements/{statementId}/pay",
            new { paidDate = "2026-10-17", actualAmount = 300m });

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
