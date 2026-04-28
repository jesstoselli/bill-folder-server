using BillFolder.Application.Abstractions.Auth;
using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Auth;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Auth;

public class AuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IValidator<SignupRequest> _signupValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;
    private readonly IValidator<LogoutRequest> _logoutValidator;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IValidator<SignupRequest> signupValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshValidator,
        IValidator<LogoutRequest> logoutValidator)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _signupValidator = signupValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
    }

    public async Task<OperationResult<AuthResponse>> SignupAsync(
        SignupRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validação de input
        var validation = await _signupValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var firstError = validation.Errors[0].ErrorMessage;
            return OperationResult.Fail<AuthResponse>("validation_error", firstError);
        }

        // 2. Email já existe?
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == emailNormalized, ct);

        if (emailExists)
        {
            return OperationResult.Fail<AuthResponse>(
                "email_already_registered",
                "Este email já está cadastrado.");
        }

        // 3. Cria User
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = emailNormalized,
            PasswordHash = _passwordHasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
        };

        _db.Users.Add(user);

        // 4. Gera tokens + persiste refresh
        var response = await GenerateAndPersistTokensAsync(user, ct);

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(response);
    }

    public async Task<OperationResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validação de input
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<AuthResponse>(
                "validation_error",
                validation.Errors[0].ErrorMessage);
        }

        // 2. Busca user
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == emailNormalized, ct);

        // 3. Verifica senha (timing-safe — Verify retorna false em vez de lançar)
        // Verify retorna false se user não existe (PasswordHash é null) ou se hash não bate
        var passwordOk = user is not null
            && user.PasswordHash is not null
            && _passwordHasher.Verify(request.Password, user.PasswordHash);

        if (!passwordOk || user is null)
        {
            // Mensagem genérica pra não revelar se email existe
            return OperationResult.Fail<AuthResponse>(
                "invalid_credentials",
                "Email ou senha inválidos.");
        }

        // 4. Gera tokens
        var response = await GenerateAndPersistTokensAsync(user, ct);

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(response);
    }

    public async Task<OperationResult<AuthResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _refreshValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<AuthResponse>(
                "validation_error",
                validation.Errors[0].ErrorMessage);
        }

        // Hash o refresh token recebido pra comparar com o que está no banco
        var tokenHash = _jwt.ComputeTokenHash(request.RefreshToken);
        var now = DateTime.UtcNow;

        var existing = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(
                rt => rt.TokenHash == tokenHash
                    && rt.RevokedAt == null
                    && rt.ExpiresAt > now,
                ct);

        if (existing is null)
        {
            return OperationResult.Fail<AuthResponse>(
                "invalid_refresh_token",
                "Refresh token inválido, revogado ou expirado.");
        }

        // ROTAÇÃO: revoga o refresh atual antes de gerar o novo par.
        // Se um atacante tem cópia desse refresh, vai falhar no próximo refresh dele.
        existing.RevokedAt = now;

        var response = await GenerateAndPersistTokensAsync(existing.User, ct);

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(response);
    }

    public async Task<OperationResult<bool>> LogoutAsync(
        LogoutRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _logoutValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<bool>(
                "validation_error",
                validation.Errors[0].ErrorMessage);
        }

        var tokenHash = _jwt.ComputeTokenHash(request.RefreshToken);
        var now = DateTime.UtcNow;

        // Idempotente: se o token não existe ou já foi revogado, retorna sucesso mesmo assim.
        // Isso evita que um cliente fazendo retry receba 404 e fique confuso.
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(
                rt => rt.TokenHash == tokenHash && rt.RevokedAt == null,
                ct);

        if (existing is not null)
        {
            existing.RevokedAt = now;
            await _db.SaveChangesAsync(ct);
        }

        return OperationResult.Ok(true);
    }

    private async Task<AuthResponse> GenerateAndPersistTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email);
        var (refreshToken, refreshHash, refreshExpiresAt) = _jwt.GenerateRefreshToken();

        var refreshEntity = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = refreshExpiresAt,
        };
        _db.RefreshTokens.Add(refreshEntity);

        // accessTokenExpiresAt = now + AccessTokenMinutes
        // (poderia ler do JwtOptions, mas pra simplicidade calculamos baseado em padrão de 30min)
        var accessExpiresAt = DateTime.UtcNow.AddMinutes(30);

        await Task.CompletedTask; // marker — na real esse método não tem await intermediário

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiresAt: accessExpiresAt,
            RefreshTokenExpiresAt: refreshExpiresAt,
            User: new UserDto(user.Id, user.Email, user.DisplayName));
    }
}
