using BillFolder.Application.Abstractions.Auth;
using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.UseCases.Auth;
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
            options.UseNpgsql(dataSource));

        // Expor IApplicationDbContext apontando pra mesma instância scoped do DbContext
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ---- JWT options ----
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // ---- Auth services ----
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<AuthService>();

        // ---- FluentValidation ----
        services.AddValidatorsFromAssemblyContaining<SignupRequestValidator>();

        return services;
    }
}
