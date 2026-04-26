using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CycleAdjustmentConfiguration : IEntityTypeConfiguration<CycleAdjustment>
{
    public void Configure(EntityTypeBuilder<CycleAdjustment> builder)
    {
        builder.ToTable("cycle_adjustments", t =>
        {
            t.HasCheckConstraint("ck_amount_nonneg", "amount >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.Label).HasColumnName("label").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.Date).HasColumnName("date");
        builder.Property(x => x.SourceSavingsTransactionId).HasColumnName("source_savings_transaction_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.CycleAdjustments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SourceSavingsTransaction)
            .WithMany()
            .HasForeignKey(x => x.SourceSavingsTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.UserId, x.Date }).HasDatabaseName("ix_cycle_adjustments_user_date");
    }
}
