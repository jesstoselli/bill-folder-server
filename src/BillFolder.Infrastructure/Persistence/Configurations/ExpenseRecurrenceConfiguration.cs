using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class ExpenseRecurrenceConfiguration : IEntityTypeConfiguration<ExpenseRecurrence>
{
    public void Configure(EntityTypeBuilder<ExpenseRecurrence> b)
    {
        b.ToTable("expense_recurrences", t =>
        {
            t.HasCheckConstraint("ck_due_day", "due_day BETWEEN 1 AND 31");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.DefaultLabel).HasColumnName("default_label").IsRequired();
        b.Property(x => x.DefaultAmount).HasColumnName("default_amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.DefaultCategoryId).HasColumnName("default_category_id");
        b.Property(x => x.DueDay).HasColumnName("due_day");
        b.Property(x => x.StartDate).HasColumnName("start_date");
        b.Property(x => x.EndDate).HasColumnName("end_date");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.ExpenseRecurrences)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.DefaultCategory)
            .WithMany()
            .HasForeignKey(x => x.DefaultCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.UserId).HasDatabaseName("ix_expense_recurrences_user");
    }
}
