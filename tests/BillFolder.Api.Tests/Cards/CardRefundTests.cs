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

namespace BillFolder.Api.Tests.Cards;

/// <summary>
/// Um estorno é um lançamento de crédito no cartão: valor negativo, sempre à
/// vista, abatendo da fatura em que cai. Fatura que fica zerada ou com crédito
/// não tem o que pagar.
/// </summary>
public sealed class CardRefundTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery-staple";

    // 2026-10-24 12:00 em São Paulo: a fatura que fechou em 10/10 já está fechada.
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-24T15:00:00Z", CultureInfo.InvariantCulture);

    private readonly HttpClient _client;

    public CardRefundTests(ApiWebApplicationFactory factory)
    {
        _client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now))))
            .CreateClient();
    }

    [Fact]
    public async Task Refund_reduces_the_statement_it_falls_in()
    {
        var (token, cardId, categoryId) = await SetupAsync();

        (await CreateEntryAsync(token, cardId, categoryId, "Tênis", 300m, 1))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var refund = await CreateEntryAsync(token, cardId, categoryId, "Estorno tênis", -100m, 1);

        refund.StatusCode.Should().Be(HttpStatusCode.Created, await refund.Content.ReadAsStringAsync());
        (await StatementTotalAsync(token, cardId)).Should().Be(200m);
    }

    [Fact]
    public async Task Refund_in_installments_is_rejected()
    {
        var (token, cardId, categoryId) = await SetupAsync();

        var response = await CreateEntryAsync(token, cardId, categoryId, "Estorno", -100m, 2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("à vista");
    }

    [Fact]
    public async Task Zero_amount_is_rejected()
    {
        var (token, cardId, categoryId) = await SetupAsync();

        var response = await CreateEntryAsync(token, cardId, categoryId, "Nada", 0m, 1);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Statement_left_with_credit_has_nothing_to_pay()
    {
        var (token, cardId, categoryId) = await SetupAsync();
        await CreateEntryAsync(token, cardId, categoryId, "Camiseta", 100m, 1);
        await CreateEntryAsync(token, cardId, categoryId, "Estorno maior", -150m, 1);
        var statementId = await StatementIdAsync(token, cardId);

        var response = await SendAsync(
            token,
            HttpMethod.Post,
            $"/v1/card-statements/{statementId}/pay",
            new { paidDate = "2026-10-17", actualAmount = 10m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("nothing_to_pay");
    }

    private async Task<(string Token, Guid CardId, Guid CategoryId)> SetupAsync()
    {
        var signup = await _client.PostAsJsonAsync(
            "/v1/auth/signup",
            new
            {
                email = $"card-refund-{Guid.NewGuid():N}@example.com",
                password = Password,
                displayName = "Card Refund Test",
            });
        signup.EnsureSuccessStatusCode();
        var token = (await signup.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        var card = await SendAsync(
            token,
            HttpMethod.Post,
            "/v1/credit-card-accounts/",
            new { name = "Nubank", closingDay = 10, dueDay = 17 });
        card.StatusCode.Should().Be(HttpStatusCode.Created, await card.Content.ReadAsStringAsync());
        using var cardBody = JsonDocument.Parse(await card.Content.ReadAsStringAsync());

        var categories = await SendAsync(token, HttpMethod.Get, "/v1/categories/");
        using var categoriesBody = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());

        return (
            token,
            cardBody.RootElement.GetProperty("id").GetGuid(),
            categoriesBody.RootElement[0].GetProperty("id").GetGuid());
    }

    private Task<HttpResponseMessage> CreateEntryAsync(
        string token, Guid cardId, Guid categoryId, string label, decimal amount, short installments) =>
        SendAsync(
            token,
            HttpMethod.Post,
            "/v1/card-entries/",
            new
            {
                cardId,
                purchaseDate = "2026-09-15",
                label,
                totalAmount = amount,
                installmentsCount = installments,
                categoryId,
            });

    private async Task<JsonElement> StatementAsync(string token, Guid cardId)
    {
        var response = await SendAsync(token, HttpMethod.Get, $"/v1/card-statements/?cardId={cardId}");
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.EnumerateArray().Single().Clone();
    }

    private async Task<decimal> StatementTotalAsync(string token, Guid cardId) =>
        (await StatementAsync(token, cardId)).GetProperty("totalAmount").GetDecimal();

    private async Task<Guid> StatementIdAsync(string token, Guid cardId) =>
        (await StatementAsync(token, cardId)).GetProperty("id").GetGuid();

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
