using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillFolder.Api.Tests.Infrastructure;
using BillFolder.Application.Dtos.Accounts;
using BillFolder.Application.Dtos.Auth;
using BillFolder.Domain.Entities;
using BillFolder.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFolder.Api.Tests.Accounts;

public sealed class CheckingAccountDeletionTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CheckingAccountDeletionTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Delete_UnusedAccount_RemovesIt()
    {
        var (_, accessToken) = await SignupAsync();
        var account = await CreateAccountAsync(accessToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/v1/checking-accounts/{account.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CheckingAccounts.AnyAsync(x => x.Id == account.Id)).Should().BeFalse();
    }

    [Theory]
    [InlineData(BlockingDependency.SavingsAccount)]
    [InlineData(BlockingDependency.DailyExpense)]
    [InlineData(BlockingDependency.DailyExpenseRecurrence)]
    public async Task Delete_AccountInUse_ReturnsConflictWithoutRemovingData(
        object dependencyValue)
    {
        var dependency = (BlockingDependency)dependencyValue;
        var (userId, accessToken) = await SignupAsync();
        var account = await CreateAccountAsync(accessToken);
        var dependencyId = await AddBlockingDependencyAsync(userId, account.Id, dependency);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/v1/checking-accounts/{account.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        problem!.Error.Should().Be("account_in_use");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CheckingAccounts.AnyAsync(x => x.Id == account.Id)).Should().BeTrue();
        (await BlockingDependencyExistsAsync(db, dependency, dependencyId)).Should().BeTrue();
    }

    private async Task<(Guid UserId, string AccessToken)> SignupAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/signup",
            new
            {
                email = $"checking-account-delete-{Guid.NewGuid():N}@example.com",
                password = Password,
                displayName = "Checking Account Delete Test",
            });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        return (auth!.User.Id, auth.AccessToken);
    }

    private async Task<CheckingAccountResponse> CreateAccountAsync(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/v1/checking-accounts/",
                new
                {
                    bankName = "Banco Teste",
                    branch = "0001",
                    accountNumber = Guid.NewGuid().ToString("N")[..20],
                    initialBalance = 100m,
                    isPrimary = false,
                });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var account = await response.Content.ReadFromJsonAsync<CheckingAccountResponse>();
            account.Should().NotBeNull();
            return account!;
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
        }
    }

    private async Task<Guid> AddBlockingDependencyAsync(
        Guid userId,
        Guid accountId,
        BlockingDependency dependency)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dependencyId = Guid.CreateVersion7();

        switch (dependency)
        {
            case BlockingDependency.SavingsAccount:
                db.SavingsAccounts.Add(new SavingsAccount
                {
                    Id = dependencyId,
                    UserId = userId,
                    CheckingAccountId = accountId,
                    BankName = "Poupanca Teste",
                    Branch = "0001",
                    AccountNumber = "12345-6",
                    InitialBalance = 50m,
                });
                break;

            case BlockingDependency.DailyExpense:
            {
                var categoryId = await db.Categories.Select(x => x.Id).FirstAsync();
                db.DailyExpenses.Add(new DailyExpense
                {
                    Id = dependencyId,
                    UserId = userId,
                    Date = new DateOnly(2026, 10, 9),
                    CategoryId = categoryId,
                    Label = "Despesa avulsa teste",
                    Amount = 25m,
                    AccountId = accountId,
                });
                break;
            }

            case BlockingDependency.DailyExpenseRecurrence:
            {
                var categoryId = await db.Categories.Select(x => x.Id).FirstAsync();
                db.DailyExpenseRecurrences.Add(new DailyExpenseRecurrence
                {
                    Id = dependencyId,
                    UserId = userId,
                    DefaultLabel = "Recorrencia teste",
                    DefaultAmount = 30m,
                    DefaultCategoryId = categoryId,
                    DefaultAccountId = accountId,
                    DayOfMonth = 9,
                    StartDate = new DateOnly(2026, 10, 9),
                });
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(dependency), dependency, null);
        }

        await db.SaveChangesAsync();
        return dependencyId;
    }

    private static Task<bool> BlockingDependencyExistsAsync(
        ApplicationDbContext db,
        BlockingDependency dependency,
        Guid dependencyId)
    {
        return dependency switch
        {
            BlockingDependency.SavingsAccount =>
                db.SavingsAccounts.AnyAsync(x => x.Id == dependencyId),
            BlockingDependency.DailyExpense =>
                db.DailyExpenses.AnyAsync(x => x.Id == dependencyId),
            BlockingDependency.DailyExpenseRecurrence =>
                db.DailyExpenseRecurrences.AnyAsync(x => x.Id == dependencyId),
            _ => throw new ArgumentOutOfRangeException(nameof(dependency), dependency, null),
        };
    }

    private sealed record ErrorResponse(string Error, string Message);

    private enum BlockingDependency
    {
        SavingsAccount,
        DailyExpense,
        DailyExpenseRecurrence,
    }
}
