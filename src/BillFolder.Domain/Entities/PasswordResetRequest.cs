namespace BillFolder.Domain.Entities;

/// <summary>
/// Pedido de reset de senha via código de 6 dígitos enviado por email.
///
/// Ciclo de vida:
///  - User pede reset em /v1/auth/forgot-password → criamos um registro
///    com CodeHash (SHA-256 do código que enviamos por email), ExpiresAt
///    = NOW() + 15min e UsedAt = null.
///  - User chega no app, digita o código + nova senha em
///    /v1/auth/reset-password. Validamos ExpiresAt > NOW() e UsedAt is null.
///    Se OK, atualizamos PasswordHash do User, marcamos UsedAt = NOW() e
///    invalidamos todos os refresh tokens do user (segurança: outros
///    devices precisam re-logar).
///
/// Quando user pede um novo reset com outro pendente, marcamos os anteriores
/// como UsedAt = NOW() pra invalidar (single-use, single-active).
///
/// O código em si NUNCA é persistido — só o hash, igual à senha. Mesmo
/// padrão de RefreshToken.
/// </summary>
public class PasswordResetRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash SHA-256 do código de 6 dígitos enviado ao usuário.
    /// </summary>
    public string CodeHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
}
