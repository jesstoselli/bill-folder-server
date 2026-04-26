using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CheckingAccountConfiguration : IEntityTypeConfiguration<CheckingAccount>
{
    public void Configure(EntityTypeBuilder<CheckingAccount> builder)
    {
        builder.ToTable("checking_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.BankName).HasColumnName("bank_name").IsRequired();
        builder.Property(x => x.Branch).HasColumnName("branch");
        builder.Property(x => x.AccountNumber).HasColumnName("account_number");
        builder.Property(x => x.InitialBalance).HasColumnName("initial_balance").HasColumnType("numeric(12,2)").HasDefaultValue(0);
        builder.Property(x => x.IsPrimary).HasColumnName("is_primary").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.CheckingAccounts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_checking_user");
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("uq_checking_one_primary_per_user")
            .HasFilter("is_primary = true");
    }
}
