using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class IncomeEntryConfiguration : IEntityTypeConfiguration<IncomeEntry>
{
    public void Configure(EntityTypeBuilder<IncomeEntry> builder)
    {
        builder.ToTable("income_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.ExpectedAmount).HasColumnName("expected_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.ActualAmount).HasColumnName("actual_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.ExpectedDate).HasColumnName("expected_date");
        builder.Property(x => x.ActualDate).HasColumnName("actual_date");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("income_status");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.IncomeEntries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Source)
            .WithMany(s => s.Entries)
            .HasForeignKey(x => x.SourceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.UserId, x.ExpectedDate }).HasDatabaseName("ix_income_entries_user_date");
        builder.HasIndex(x => x.SourceId).HasDatabaseName("ix_income_entries_source");
    }
}
