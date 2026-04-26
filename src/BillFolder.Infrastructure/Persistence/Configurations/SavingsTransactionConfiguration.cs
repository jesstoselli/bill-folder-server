using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class SavingsTransactionConfiguration : IEntityTypeConfiguration<SavingsTransaction>
{
    public void Configure(EntityTypeBuilder<SavingsTransaction> builder)
    {
        builder.ToTable("savings_transactions", t =>
        {
            t.HasCheckConstraint("ck_amount_nonneg", "amount >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.SavingsAccountId).HasColumnName("savings_account_id");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.Date).HasColumnName("date");
        builder.Property(x => x.Label).HasColumnName("label");
        builder.Property(x => x.LinkedTransactionId).HasColumnName("linked_transaction_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.SavingsTransactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SavingsAccount)
            .WithMany(a => a.Transactions)
            .HasForeignKey(x => x.SavingsAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LinkedTransaction)
            .WithMany() // self-ref, sem coleção reversa
            .HasForeignKey(x => x.LinkedTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.SavingsAccountId, x.Date }).HasDatabaseName("ix_savings_transactions_account_date");
        builder.HasIndex(x => new { x.UserId, x.Date }).HasDatabaseName("ix_savings_transactions_user_date");
    }
}
