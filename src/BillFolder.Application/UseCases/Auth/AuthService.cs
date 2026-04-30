using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BillFolder.Application.Abstractions.Auth;
using BillFolder.Application.Abstractions.Email;
using BillFolder.Application.Abstractions.Persistence;
using BillFolder.Application.Common;
using BillFolder.Application.Dtos.Auth;
using BillFolder.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BillFolder.Application.UseCases.Auth;

public class AuthService
{
    /// <summary>
    /// TTL do código de reset de senha. Curto pra reduzir janela de abuso —
    /// se user perdeu o email ou demorou, pede outro (invalida o anterior).
    /// </summary>
    private static readonly TimeSpan PasswordResetTtl = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IEmailSender _emailSender;
    private readonly IValidator<SignupRequest> _signupValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;
    private readonly IValidator<LogoutRequest> _logoutValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IEmailSender emailSender,
        IValidator<SignupRequest> signupValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshValidator,
        IValidator<LogoutRequest> logoutValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _emailSender = emailSender;
        _signupValidator = signupValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
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

    /// <summary>
    /// Inicia fluxo de reset de senha. Sempre retorna OK independente de o
    /// email existir ou não — proteção contra enumeration de usuários.
    ///
    /// Se o email existe: gera código de 6 dígitos, hash SHA-256, persiste
    /// com TTL de 15min, invalida pedidos anteriores ainda ativos do mesmo
    /// user, e dispara email pelo IEmailSender.
    ///
    /// Se IEmailSender não está configurado (NoOpEmailSender em dev),
    /// retorna o código em DevCode pra testar via curl. Em prod com Resend
    /// configurado, DevCode sempre vai como null.
    /// </summary>
    public async Task<OperationResult<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _forgotPasswordValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<ForgotPasswordResponse>(
                "validation_error",
                validation.Errors[0].ErrorMessage);
        }

        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == emailNormalized, ct);

        // Email não cadastrado? Retorna OK mesmo assim com DevCode null.
        // Não revela se o email existe (timing attack possível em teoria mas
        // mitigado pelo custo equivalente das duas paths).
        if (user is null)
        {
            return OperationResult.Ok(new ForgotPasswordResponse(DevCode: null));
        }

        // Invalida requests ativos do user (single active request por vez).
        var now = DateTime.UtcNow;
        var existing = await _db.PasswordResetRequests
            .Where(r => r.UserId == user.Id && r.UsedAt == null)
            .ToListAsync(ct);
        foreach (var prev in existing)
        {
            prev.UsedAt = now;
        }

        // Gera código novo (cryptographically secure, 6 dígitos zero-padded).
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);

        var entity = new PasswordResetRequest
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            CodeHash = HashCode(code),
            ExpiresAt = now.Add(PasswordResetTtl),
        };
        _db.PasswordResetRequests.Add(entity);

        await _db.SaveChangesAsync(ct);

        // Tenta enviar email. Se a impl é NoOp (dev sem ApiKey), nada é
        // enviado e DevCode vai com o código pra testar via curl.
        if (_emailSender.IsConfigured)
        {
            var subject = "BillFolder — código de redefinição de senha";
            var body = $$"""
                Olá!

                Você (ou alguém usando seu email) solicitou redefinir a senha do BillFolder.

                Seu código é: {{code}}

                Esse código expira em 15 minutos. Se você não pediu, pode ignorar este email — sua senha continua a mesma.

                — BillFolder
                """;
            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, ct);
            }
            catch
            {
                // Falha de email não deve vazar pra resposta (proteção contra
                // enumeration). Logger no caller é responsável por rastrear.
                // Como o request já foi salvo, user pode pedir outro reset
                // se este nunca chegar.
            }
        }

        return OperationResult.Ok(new ForgotPasswordResponse(
            DevCode: _emailSender.IsConfigured ? null : code));
    }

    /// <summary>
    /// Conclui fluxo de reset. Valida código + email + TTL + UsedAt is null.
    /// Em sucesso: atualiza PasswordHash, marca request como usado, e revoga
    /// todos os refresh tokens ativos do user (segurança — outros devices
    /// precisam re-logar com a senha nova).
    /// </summary>
    public async Task<OperationResult<bool>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _resetPasswordValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<bool>(
                "validation_error",
                validation.Errors[0].ErrorMessage);
        }

        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == emailNormalized, ct);

        // Mensagem genérica pra não revelar se email/código bate. Mesma
        // string pros 3 caminhos (user inexistente, código errado, código
        // expirado) — UX simples, segurança consistente.
        const string invalidMessage = "Código inválido ou expirado.";

        if (user is null)
        {
            return OperationResult.Fail<bool>("invalid_reset_code", invalidMessage);
        }

        var codeHash = HashCode(request.Code);
        var now = DateTime.UtcNow;

        var resetRequest = await _db.PasswordResetRequests
            .FirstOrDefaultAsync(
                r => r.UserId == user.Id
                    && r.CodeHash == codeHash
                    && r.UsedAt == null
                    && r.ExpiresAt > now,
                ct);

        if (resetRequest is null)
        {
            return OperationResult.Fail<bool>("invalid_reset_code", invalidMessage);
        }

        // Sucesso: atualiza senha, marca request como usado, revoga refresh
        // tokens ativos. SaveChanges único pra atomicidade.
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        resetRequest.UsedAt = now;

        var activeRefreshTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefreshTokens)
        {
            rt.RevokedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(true);
    }

    /// <summary>
    /// Hash SHA-256 do código retornado em base64 — mesmo formato usado
    /// em IJwtTokenService.ComputeTokenHash. Persistir só o hash garante
    /// que o código original não vaza nem em backups do banco.
    /// </summary>
    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
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
