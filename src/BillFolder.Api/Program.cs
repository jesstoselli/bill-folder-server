using System.Text;
using BillFolder.Api.Endpoints;
using BillFolder.Infrastructure;
using BillFolder.Infrastructure.Auth;
using BillFolder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// ============================================================================
// JWT Bearer authentication
// ============================================================================
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOpts = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
app.MapUsersEndpoints();
app.MapCategoriesEndpoints();
app.MapCheckingAccountsEndpoints();

app.Run();
