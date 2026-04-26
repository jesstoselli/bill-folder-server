using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CheckingAccountConfiguration : IEntityTypeConfiguration<CheckingAccount>
{
    public void Configure(EntityTypeBuilder<CheckingAccount> b)
    {
        b.ToTable("checking_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.BankName).HasColumnName("bank_name").IsRequired();
        b.Property(x => x.Branch).HasColumnName("branch");
        b.Property(x => x.AccountNumber).HasColumnName("account_number");
        b.Property(x => x.InitialBalance).HasColumnName("initial_balance").HasColumnType("numeric(12,2)").HasDefaultValue(0);
        b.Property(x => x.IsPrimary).HasColumnName("is_primary").HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.CheckingAccounts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.UserId).HasDatabaseName("ix_checking_user");
        b.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("uq_checking_one_primary_per_user")
            .HasFilter("is_primary = true");
    }
}
