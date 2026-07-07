using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class ExpenseRecurrenceConfiguration : IEntityTypeConfiguration<ExpenseRecurrence>
{
    public void Configure(EntityTypeBuilder<ExpenseRecurrence> builder)
    {
        builder.ToTable("expense_recurrences", t =>
        {
            t.HasCheckConstraint("ck_due_day", "due_day IS NULL OR (due_day BETWEEN 1 AND 31)");
            t.HasCheckConstraint("ck_weekday", "weekday IS NULL OR (weekday BETWEEN 0 AND 6)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.DefaultLabel).HasColumnName("default_label").IsRequired();
        builder.Property(x => x.DefaultAmount).HasColumnName("default_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.DefaultCategoryId).HasColumnName("default_category_id");
        // Sem HasDefaultValue no banco (mesma pegada do Status em ExpenseConfiguration:
        // default em coluna enum postgres disparava RETURNING bugado). O default é
        // C#-side (Monthly) e a Application sempre seta Frequency explicitamente.
        builder.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasColumnType("expense_recurrence_frequency");
        builder.Property(x => x.DueDay).HasColumnName("due_day");
        builder.Property(x => x.Weekday).HasColumnName("weekday");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.ExpenseRecurrences)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DefaultCategory)
            .WithMany()
            .HasForeignKey(x => x.DefaultCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_expense_recurrences_user");
    }
}
