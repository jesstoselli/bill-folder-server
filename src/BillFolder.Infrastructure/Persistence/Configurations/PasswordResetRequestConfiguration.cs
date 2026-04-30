using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class PasswordResetRequestConfiguration : IEntityTypeConfiguration<PasswordResetRequest>
{
    public void Configure(EntityTypeBuilder<PasswordResetRequest> builder)
    {
        builder.ToTable("password_reset_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.PasswordResetRequests)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lookup: queries do reset filtram por user + ativo (used_at IS NULL).
        // Index composto pra atender o caso comum sem precisar full scan.
        builder.HasIndex(x => new { x.UserId, x.UsedAt })
            .HasDatabaseName("ix_password_reset_requests_user_id_used_at");

        // Lookup: validação do code recebido. CodeHash não precisa ser unique
        // (collisions virtualmente impossíveis em SHA-256, mas teoricamente
        // vários users poderiam ter mesmo código ativo). Index simples basta.
        builder.HasIndex(x => x.CodeHash)
            .HasDatabaseName("ix_password_reset_requests_code_hash");
    }
}
