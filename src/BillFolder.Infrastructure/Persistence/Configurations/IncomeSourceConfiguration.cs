using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class IncomeSourceConfiguration : IEntityTypeConfiguration<IncomeSource>
{
    public void Configure(EntityTypeBuilder<IncomeSource> b)
    {
        b.ToTable("income_sources", t =>
        {
            t.HasCheckConstraint("ck_expected_day", "expected_day BETWEEN 1 AND 31");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Origin).HasColumnName("origin").IsRequired();
        b.Property(x => x.OriginType).HasColumnName("origin_type"); // mapeado como ENUM no DbContext
        b.Property(x => x.DefaultAmount).HasColumnName("default_amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.ExpectedDay).HasColumnName("expected_day");
        b.Property(x => x.StartDate).HasColumnName("start_date");
        b.Property(x => x.EndDate).HasColumnName("end_date");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.IncomeSources)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.UserId).HasDatabaseName("ix_income_sources_user");
    }
}
