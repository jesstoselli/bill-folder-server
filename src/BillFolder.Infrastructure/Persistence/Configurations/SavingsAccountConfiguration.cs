using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class SavingsAccountConfiguration : IEntityTypeConfiguration<SavingsAccount>
{
    public void Configure(EntityTypeBuilder<SavingsAccount> b)
    {
        b.ToTable("savings_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.CheckingAccountId).HasColumnName("checking_account_id");
        b.Property(x => x.BankName).HasColumnName("bank_name").IsRequired();
        b.Property(x => x.Branch).HasColumnName("branch");
        b.Property(x => x.AccountNumber).HasColumnName("account_number");
        b.Property(x => x.InitialBalance).HasColumnName("initial_balance").HasColumnType("numeric(12,2)").HasDefaultValue(0);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.SavingsAccounts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.CheckingAccount)
            .WithOne(c => c.SavingsAccount)
            .HasForeignKey<SavingsAccount>(x => x.CheckingAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.CheckingAccountId).IsUnique();
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_savings_user");
    }
}
