using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CardStatementConfiguration : IEntityTypeConfiguration<CardStatement>
{
    public void Configure(EntityTypeBuilder<CardStatement> b)
    {
        b.ToTable("card_statements");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.CardId).HasColumnName("card_id");
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");
        b.Property(x => x.DueDate).HasColumnName("due_date");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.LinkedExpenseId).HasColumnName("linked_expense_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.CardStatements)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Card)
            .WithMany(c => c.Statements)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.LinkedExpense)
            .WithMany() // não tem coleção reversa pra evitar ambiguidade
            .HasForeignKey(x => x.LinkedExpenseId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.CardId, x.DueDate }).HasDatabaseName("ix_card_statements_card_due");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_card_statements_user");
    }
}
