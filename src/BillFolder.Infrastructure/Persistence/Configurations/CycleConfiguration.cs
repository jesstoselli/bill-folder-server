using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CycleConfiguration : IEntityTypeConfiguration<Cycle>
{
    public void Configure(EntityTypeBuilder<Cycle> b)
    {
        b.ToTable("cycles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.StartDate).HasColumnName("start_date");
        b.Property(x => x.EndDate).HasColumnName("end_date");
        b.Property(x => x.Label).HasColumnName("label").IsRequired();
        b.Property(x => x.IsRecurrenceGenerated).HasColumnName("is_recurrence_generated").HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.Cycles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.StartDate }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.StartDate, x.EndDate }).HasDatabaseName("ix_cycles_user_dates");
    }
}
