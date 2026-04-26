using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CycleAdjustmentConfiguration : IEntityTypeConfiguration<CycleAdjustment>
{
    public void Configure(EntityTypeBuilder<CycleAdjustment> b)
    {
        b.ToTable("cycle_adjustments", t =>
        {
            t.HasCheckConstraint("ck_amount_nonneg", "amount >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Type).HasColumnName("type");
        b.Property(x => x.Label).HasColumnName("label").IsRequired();
        b.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.Date).HasColumnName("date");
        b.Property(x => x.SourceSavingsTransactionId).HasColumnName("source_savings_transaction_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.CycleAdjustments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.SourceSavingsTransaction)
            .WithMany()
            .HasForeignKey(x => x.SourceSavingsTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.UserId, x.Date }).HasDatabaseName("ix_cycle_adjustments_user_date");
    }
}
