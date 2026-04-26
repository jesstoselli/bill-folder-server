using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> b)
    {
        b.ToTable("installments", t =>
        {
            t.HasCheckConstraint("ck_installment_number_min1", "installment_number >= 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.CardEntryId).HasColumnName("card_entry_id");
        b.Property(x => x.StatementId).HasColumnName("statement_id");
        b.Property(x => x.InstallmentNumber).HasColumnName("installment_number");
        b.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");

        b.HasOne(x => x.CardEntry)
            .WithMany(e => e.Installments)
            .HasForeignKey(x => x.CardEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Statement)
            .WithMany(s => s.Installments)
            .HasForeignKey(x => x.StatementId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.CardEntryId, x.InstallmentNumber }).IsUnique();
        b.HasIndex(x => x.StatementId).HasDatabaseName("ix_installments_statement");
    }
}
