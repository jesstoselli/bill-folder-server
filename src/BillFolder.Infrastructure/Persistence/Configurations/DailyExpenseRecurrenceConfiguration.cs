using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class DailyExpenseRecurrenceConfiguration : IEntityTypeConfiguration<DailyExpenseRecurrence>
{
    public void Configure(EntityTypeBuilder<DailyExpenseRecurrence> builder)
    {
        builder.ToTable("daily_expense_recurrences", t =>
        {
            t.HasCheckConstraint("ck_day_of_month", "day_of_month BETWEEN 1 AND 31");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.DefaultLabel).HasColumnName("default_label").IsRequired();
        builder.Property(x => x.DefaultAmount).HasColumnName("default_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.DefaultCategoryId).HasColumnName("default_category_id");
        builder.Property(x => x.DefaultAccountId).HasColumnName("default_account_id");
        builder.Property(x => x.DayOfMonth).HasColumnName("day_of_month");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.DailyExpenseRecurrences)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DefaultCategory)
            .WithMany()
            .HasForeignKey(x => x.DefaultCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DefaultAccount)
            .WithMany(a => a.DailyExpenseRecurrences)
            .HasForeignKey(x => x.DefaultAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_daily_expense_recurrences_user");
    }
}
