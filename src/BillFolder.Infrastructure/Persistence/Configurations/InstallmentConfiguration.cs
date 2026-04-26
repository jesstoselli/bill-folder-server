using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> builder)
    {
        builder.ToTable("installments", t =>
        {
            t.HasCheckConstraint("ck_installment_number_min1", "installment_number >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CardEntryId).HasColumnName("card_entry_id");
        builder.Property(x => x.StatementId).HasColumnName("statement_id");
        builder.Property(x => x.InstallmentNumber).HasColumnName("installment_number");
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");

        builder.HasOne(x => x.CardEntry)
            .WithMany(e => e.Installments)
            .HasForeignKey(x => x.CardEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Statement)
            .WithMany(s => s.Installments)
            .HasForeignKey(x => x.StatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CardEntryId, x.InstallmentNumber }).IsUnique();
        builder.HasIndex(x => x.StatementId).HasDatabaseName("ix_installments_statement");
    }
}
