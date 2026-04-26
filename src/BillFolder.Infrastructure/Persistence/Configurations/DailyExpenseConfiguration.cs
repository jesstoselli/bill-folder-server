using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class DailyExpenseConfiguration : IEntityTypeConfiguration<DailyExpense>
{
    public void Configure(EntityTypeBuilder<DailyExpense> b)
    {
        b.ToTable("daily_expenses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.TemplateId).HasColumnName("template_id");
        b.Property(x => x.Date).HasColumnName("date");
        b.Property(x => x.CategoryId).HasColumnName("category_id");
        b.Property(x => x.Label).HasColumnName("label").IsRequired();
        b.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.AccountId).HasColumnName("account_id");
        b.Property(x => x.Notes).HasColumnName("notes");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.DailyExpenses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Template)
            .WithMany(t => t.Instances)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Account)
            .WithMany(a => a.DailyExpenses)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.Date }).HasDatabaseName("ix_daily_expenses_user_date");
        b.HasIndex(x => x.TemplateId).HasDatabaseName("ix_daily_expenses_template");
    }
}
