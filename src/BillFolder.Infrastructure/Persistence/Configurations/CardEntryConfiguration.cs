using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CardEntryConfiguration : IEntityTypeConfiguration<CardEntry>
{
    public void Configure(EntityTypeBuilder<CardEntry> b)
    {
        b.ToTable("card_entries", t =>
        {
            t.HasCheckConstraint("ck_total_amount_nonneg", "total_amount >= 0");
            t.HasCheckConstraint("ck_installments_count_min1", "installments_count >= 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.CardId).HasColumnName("card_id");
        b.Property(x => x.TemplateId).HasColumnName("template_id");
        b.Property(x => x.PurchaseDate).HasColumnName("purchase_date");
        b.Property(x => x.Label).HasColumnName("label").IsRequired();
        b.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.InstallmentsCount).HasColumnName("installments_count");
        b.Property(x => x.CategoryId).HasColumnName("category_id");
        b.Property(x => x.Notes).HasColumnName("notes");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany(u => u.CardEntries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Card)
            .WithMany(c => c.CardEntries)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Template)
            .WithMany(t => t.Instances)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.UserId, x.PurchaseDate }).HasDatabaseName("ix_card_entries_user_date");
        b.HasIndex(x => x.CardId).HasDatabaseName("ix_card_entries_card");
        b.HasIndex(x => x.TemplateId).HasDatabaseName("ix_card_entries_template");
    }
}
