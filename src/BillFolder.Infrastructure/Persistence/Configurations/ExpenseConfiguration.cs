using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.Label).HasColumnName("label").IsRequired();
        builder.Property(x => x.ExpectedAmount).HasColumnName("expected_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.ActualAmount).HasColumnName("actual_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.Status).HasColumnName("status").HasDefaultValue(ExpenseStatus.Pending);
        builder.Property(x => x.PaidDate).HasColumnName("paid_date");
        builder.Property(x => x.PaidFromAccountId).HasColumnName("paid_from_account_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.LinkedCardStatementId).HasColumnName("linked_card_statement_id");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.Expenses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Template)
            .WithMany(t => t.Instances)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PaidFromAccount)
            .WithMany(a => a.ExpensesPaidFromHere)
            .HasForeignKey(x => x.PaidFromAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LinkedCardStatement)
            .WithMany(s => s.RelatedExpenses)
            .HasForeignKey(x => x.LinkedCardStatementId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.UserId, x.DueDate }).HasDatabaseName("ix_expenses_user_due");
        builder.HasIndex(x => x.TemplateId).HasDatabaseName("ix_expenses_template");
        builder.HasIndex(x => new { x.UserId, x.Status })
            .HasFilter("status = 'overdue'")
            .HasDatabaseName("ix_expenses_status");
    }
}
