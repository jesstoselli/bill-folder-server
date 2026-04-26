using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CreditCardAccountConfiguration : IEntityTypeConfiguration<CreditCardAccount>
{
    public void Configure(EntityTypeBuilder<CreditCardAccount> b)
    {
        b.ToTable("credit_card_accounts", t =>
        {
            t.HasCheckConstraint("ck_closing_day", "closing_day BETWEEN 1 AND 31");
            t.HasCheckConstraint("ck_due_day", "due_day BETWEEN 1 AND 31");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.IssuerBank).HasColumnName("issuer_bank");
        b.Property(x => x.Brand).HasColumnName("brand");
        b.Property(x => x.ClosingDay).HasColumnName("closing_day");
        b.Property(x => x.DueDay).HasColumnName("due_day");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.CreditCardAccounts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.UserId).HasDatabaseName("ix_credit_cards_user");
    }
}
