using System.Text;
using System.Text.Json.Serialization;
using BillFolder.Api.Auth;
using BillFolder.Api.Endpoints;
using BillFolder.Infrastructure;
using BillFolder.Infrastructure.Auth;
using BillFolder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var webAuthSection = builder.Configuration.GetSection(WebAuthOptions.SectionName);
builder.Services.AddSingleton<IValidateOptions<WebAuthOptions>, WebAuthOptionsValidator>();
builder.Services.AddOptions<WebAuthOptions>()
    .Bind(webAuthSection)
    .ValidateOnStart();
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IOptions<WebAuthOptions>>((options, configuredWebAuth) =>
        options.AddPolicy("BillFolderWeb", policy =>
            policy.WithOrigins(configuredWebAuth.Value.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

// JSON: enums serializam como string camelCase ("pending", "paid", "overdue")
// em vez de número (0/1/2). Configurável também na deserialização.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});

// ============================================================================
// JWT Bearer authentication
// ============================================================================
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, configuredJwt) =>
    {
        var jwtOpts = configuredJwt.Value;

        // Desliga o mapping legado de WS-Federation (sub → NameIdentifier).
        // Mantém os nomes RFC 7519 originais (sub, email, jti, etc).
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOpts.Issuer,
            ValidAudience = jwtOpts.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// TLS termination é feita pelo nginx em produção (e pelo Kestrel em dev).
// API em si só escuta HTTP — não precisa de UseHttpsRedirection.

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments(
            "/v1/auth/web",
            StringComparison.Ordinal)
        && !WebOriginPolicy.IsAllowed(
            context.Request,
            context.RequestServices.GetRequiredService<IOptions<WebAuthOptions>>().Value))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    await next();
});
app.UseCors("BillFolderWeb");
app.UseAuthentication();
app.UseAuthorization();

// ============================================================================
// Endpoints
// ============================================================================

app.MapGet("/v1/health", async (ApplicationDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return Results.Ok(new
    {
        status = canConnect ? "ok" : "degraded",
        version = "0.1.1",
        timestamp = DateTime.UtcNow,
    });
});

app.MapAuthEndpoints();
app.MapWebAuthEndpoints();
app.MapUsersEndpoints();
app.MapCategoriesEndpoints();
app.MapCardEntriesEndpoints();
app.MapCardStatementsEndpoints();
app.MapCheckingAccountsEndpoints();
app.MapCreditCardAccountsEndpoints();
app.MapCyclesEndpoints();
app.MapDailyExpensesEndpoints();
app.MapExpensesEndpoints();
app.MapHomeEndpoints();
app.MapIncomeSourcesEndpoints();
app.MapIncomeEntriesEndpoints();
app.MapExpenseRecurrencesEndpoints();
app.MapDailyExpenseRecurrencesEndpoints();
app.MapCardEntryRecurrencesEndpoints();
app.MapCycleAdjustmentsEndpoints();
app.MapSavingsAccountsEndpoints();
app.MapSavingsTransactionsEndpoints();

app.Run();

public partial class Program;
