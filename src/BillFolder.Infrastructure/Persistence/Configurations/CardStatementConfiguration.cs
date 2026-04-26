using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CardStatementConfiguration : IEntityTypeConfiguration<CardStatement>
{
    public void Configure(EntityTypeBuilder<CardStatement> builder)
    {
        builder.ToTable("card_statements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CardId).HasColumnName("card_id");
        builder.Property(x => x.PeriodStart).HasColumnName("period_start");
        builder.Property(x => x.PeriodEnd).HasColumnName("period_end");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.LinkedExpenseId).HasColumnName("linked_expense_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.CardStatements)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Card)
            .WithMany(c => c.Statements)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LinkedExpense)
            .WithMany() // não tem coleção reversa pra evitar ambiguidade
            .HasForeignKey(x => x.LinkedExpenseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.CardId, x.DueDate }).HasDatabaseName("ix_card_statements_card_due");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_card_statements_user");
    }
}
