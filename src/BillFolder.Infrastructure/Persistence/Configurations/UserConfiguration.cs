using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t =>
        {
            t.HasCheckConstraint("users_auth_method_chk",
                "password_hash IS NOT NULL OR google_oauth_id IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Email).HasColumnName("email").IsRequired();
        b.Property(x => x.PasswordHash).HasColumnName("password_hash");
        b.Property(x => x.GoogleOauthId).HasColumnName("google_oauth_id");
        b.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        b.Property(x => x.CycleStartRule).HasColumnName("cycle_start_rule").HasDefaultValue("5th_business_day");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.GoogleOauthId).IsUnique();
    }
}
