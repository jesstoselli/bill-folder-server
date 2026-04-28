using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class IncomeSourceConfiguration : IEntityTypeConfiguration<IncomeSource>
{
    public void Configure(EntityTypeBuilder<IncomeSource> builder)
    {
        builder.ToTable("income_sources", t =>
        {
            t.HasCheckConstraint("ck_expected_day", "expected_day BETWEEN 1 AND 31");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Origin).HasColumnName("origin").IsRequired();
        builder.Property(x => x.OriginType)
            .HasColumnName("origin_type")
            .HasColumnType("income_origin_type");
        builder.Property(x => x.DefaultAmount).HasColumnName("default_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.ExpectedDay).HasColumnName("expected_day");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.IncomeSources)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_income_sources_user");
    }
}
