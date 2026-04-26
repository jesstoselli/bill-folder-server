using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class IncomeEntryConfiguration : IEntityTypeConfiguration<IncomeEntry>
{
    public void Configure(EntityTypeBuilder<IncomeEntry> b)
    {
        b.ToTable("income_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SourceId).HasColumnName("source_id");
        b.Property(x => x.ExpectedAmount).HasColumnName("expected_amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.ActualAmount).HasColumnName("actual_amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.ExpectedDate).HasColumnName("expected_date");
        b.Property(x => x.ActualDate).HasColumnName("actual_date");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.Notes).HasColumnName("notes");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.IncomeEntries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Source)
            .WithMany(s => s.Entries)
            .HasForeignKey(x => x.SourceId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.UserId, x.ExpectedDate }).HasDatabaseName("ix_income_entries_user_date");
        b.HasIndex(x => x.SourceId).HasDatabaseName("ix_income_entries_source");
    }
}
