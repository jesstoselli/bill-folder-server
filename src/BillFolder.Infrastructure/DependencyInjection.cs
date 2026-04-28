using BillFolder.Application.Abstractions.Auth;
using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.UseCases.Accounts;
using BillFolder.Application.UseCases.Auth;
using BillFolder.Application.UseCases.Cards;
using BillFolder.Application.UseCases.CreditCards;
using BillFolder.Application.UseCases.Cycles;
using BillFolder.Application.UseCases.DailyExpenses;
using BillFolder.Application.UseCases.Expenses;
using BillFolder.Application.UseCases.Home;
using BillFolder.Application.UseCases.Incomes;
using BillFolder.Application.UseCases.Savings;
using BillFolder.Application.Validators.Auth;
using BillFolder.Domain.Enums;
using BillFolder.Infrastructure.Auth;
using BillFolder.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BillFolder.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra DbContext + serviços de auth + configurações.
    /// Chama no Program.cs com builder.Services.AddInfrastructure(builder.Configuration).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // ---- Postgres + EF Core ----
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<IncomeOriginType>("income_origin_type");
        dataSourceBuilder.MapEnum<IncomeStatus>("income_status");
        dataSourceBuilder.MapEnum<ExpenseStatus>("expense_status");
        dataSourceBuilder.MapEnum<CardStatementStatus>("card_statement_status");
        dataSourceBuilder.MapEnum<SavingsTransactionType>("savings_transaction_type");
        dataSourceBuilder.MapEnum<CycleAdjustmentType>("cycle_adjustment_type");
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                // Os enums precisam ser registrados TAMBÉM no nível do EF Core provider,
                // não só no DataSource. Sem isso, EF Core gera SQL passando inteiro pro
                // ENUM postgres e dá erro "expression is of type integer".
                npgsql.MapEnum<IncomeOriginType>("income_origin_type");
                npgsql.MapEnum<IncomeStatus>("income_status");
                npgsql.MapEnum<ExpenseStatus>("expense_status");
                npgsql.MapEnum<CardStatementStatus>("card_statement_status");
                npgsql.MapEnum<SavingsTransactionType>("savings_transaction_type");
                npgsql.MapEnum<CycleAdjustmentType>("cycle_adjustment_type");
            }));

        // Expor IApplicationDbContext apontando pra mesma instância scoped do DbContext
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ---- JWT options ----
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // ---- Auth services ----
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<CardEntriesService>();
        services.AddScoped<CardStatementsService>();
        services.AddScoped<CheckingAccountsService>();
        services.AddScoped<CreditCardAccountsService>();
        services.AddScoped<CyclesService>();
        services.AddScoped<DailyExpensesService>();
        services.AddScoped<ExpensesService>();
        services.AddScoped<HomeService>();
        services.AddScoped<IncomeSourcesService>();
        services.AddScoped<IncomeEntriesService>();
        services.AddScoped<SavingsAccountsService>();
        services.AddScoped<SavingsTransactionsService>();

        // ---- FluentValidation ----
        // Discovery automático no assembly inteiro do Application
        services.AddValidatorsFromAssemblyContaining<SignupRequestValidator>();

        return services;
    }
}
