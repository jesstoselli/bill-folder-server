namespace BillFolder.Application.Dtos.Auth;

public sealed record SignupRequest(
    string Email,
    string Password,
    string DisplayName);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshTokenRequest(
    string RefreshToken);

public sealed record LogoutRequest(
    string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName);

/// <summary>
/// Pedido de reset. Sempre retornamos 200 do endpoint independente de
/// o email existir ou não — protege contra enumeration de usuários.
/// </summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>
/// Resposta de forgot-password. DevCode é populado APENAS quando a
/// infra de email não está configurada (Resend ApiKey vazio) — útil
/// pra testar via curl em dev. Em produção, sempre null.
/// </summary>
public sealed record ForgotPasswordResponse(string? DevCode);

/// <summary>
/// Reset propriamente dito. Code é o de 6 dígitos enviado por email
/// (ou retornado em DevCode quando email não está configurado).
/// </summary>
public sealed record ResetPasswordRequest(
    string Email,
    string Code,
    string NewPassword);
