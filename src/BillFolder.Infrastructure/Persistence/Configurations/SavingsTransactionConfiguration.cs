using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class SavingsTransactionConfiguration : IEntityTypeConfiguration<SavingsTransaction>
{
    public void Configure(EntityTypeBuilder<SavingsTransaction> b)
    {
        b.ToTable("savings_transactions", t =>
        {
            t.HasCheckConstraint("ck_amount_nonneg", "amount >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SavingsAccountId).HasColumnName("savings_account_id");
        b.Property(x => x.Type).HasColumnName("type");
        b.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.Date).HasColumnName("date");
        b.Property(x => x.Label).HasColumnName("label");
        b.Property(x => x.LinkedTransactionId).HasColumnName("linked_transaction_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.SavingsTransactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.SavingsAccount)
            .WithMany(a => a.Transactions)
            .HasForeignKey(x => x.SavingsAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.LinkedTransaction)
            .WithMany() // self-ref, sem coleção reversa
            .HasForeignKey(x => x.LinkedTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.SavingsAccountId, x.Date }).HasDatabaseName("ix_savings_transactions_account_date");
        b.HasIndex(x => new { x.UserId, x.Date }).HasDatabaseName("ix_savings_transactions_user_date");
    }
}
