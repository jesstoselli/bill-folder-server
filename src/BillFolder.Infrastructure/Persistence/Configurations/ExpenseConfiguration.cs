using BillFolder.Domain.Entities;
using BillFolder.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ToTable("expenses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.TemplateId).HasColumnName("template_id");
        b.Property(x => x.DueDate).HasColumnName("due_date");
        b.Property(x => x.Label).HasColumnName("label").IsRequired();
        b.Property(x => x.ExpectedAmount).HasColumnName("expected_amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.ActualAmount).HasColumnName("actual_amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue(ExpenseStatus.Pending);
        b.Property(x => x.PaidDate).HasColumnName("paid_date");
        b.Property(x => x.PaidFromAccountId).HasColumnName("paid_from_account_id");
        b.Property(x => x.CategoryId).HasColumnName("category_id");
        b.Property(x => x.LinkedCardStatementId).HasColumnName("linked_card_statement_id");
        b.Property(x => x.Notes).HasColumnName("notes");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.Expenses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Template)
            .WithMany(t => t.Instances)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.PaidFromAccount)
            .WithMany(a => a.ExpensesPaidFromHere)
            .HasForeignKey(x => x.PaidFromAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.LinkedCardStatement)
            .WithMany(s => s.RelatedExpenses)
            .HasForeignKey(x => x.LinkedCardStatementId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.UserId, x.DueDate }).HasDatabaseName("ix_expenses_user_due");
        b.HasIndex(x => x.TemplateId).HasDatabaseName("ix_expenses_template");
        b.HasIndex(x => new { x.UserId, x.Status })
            .HasFilter("status = 'overdue'")
            .HasDatabaseName("ix_expenses_status");
    }
}
