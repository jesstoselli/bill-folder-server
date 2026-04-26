using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CreditCardAccountConfiguration : IEntityTypeConfiguration<CreditCardAccount>
{
    public void Configure(EntityTypeBuilder<CreditCardAccount> builder)
    {
        builder.ToTable("credit_card_accounts", t =>
        {
            t.HasCheckConstraint("ck_closing_day", "closing_day BETWEEN 1 AND 31");
            t.HasCheckConstraint("ck_due_day", "due_day BETWEEN 1 AND 31");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.IssuerBank).HasColumnName("issuer_bank");
        builder.Property(x => x.Brand).HasColumnName("brand");
        builder.Property(x => x.ClosingDay).HasColumnName("closing_day");
        builder.Property(x => x.DueDay).HasColumnName("due_day");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.CreditCardAccounts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_credit_cards_user");
    }
}
