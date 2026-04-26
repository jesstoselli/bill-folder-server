using BillFolder.Domain.Enums;
using BillFolder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' not configured");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<IncomeOriginType>("income_origin_type");
dataSourceBuilder.MapEnum<IncomeStatus>("income_status");
dataSourceBuilder.MapEnum<ExpenseStatus>("expense_status");
dataSourceBuilder.MapEnum<CardStatementStatus>("card_statement_status");
dataSourceBuilder.MapEnum<SavingsTransactionType>("savings_transaction_type");
dataSourceBuilder.MapEnum<CycleAdjustmentType>("cycle_adjustment_type");
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(dataSource));

var app = builder.Build();

app.UseHttpsRedirection();

// ============================================================================
// Endpoints
// ============================================================================

app.MapGet("/v1/health", async (ApplicationDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return Results.Ok(new
    {
        status = canConnect ? "ok" : "degraded",
        version = "0.1.0",
        timestamp = DateTime.UtcNow,
    });
});

app.Run();
