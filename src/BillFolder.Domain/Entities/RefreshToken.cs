namespace BillFolder.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash SHA-256 do token opaco enviado ao cliente.
    /// O token original NUNCA é persistido — só o hash, igual senha.
    /// </summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigations
    public User User { get; set; } = null!;
}
