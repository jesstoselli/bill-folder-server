using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CardEntryConfiguration : IEntityTypeConfiguration<CardEntry>
{
    public void Configure(EntityTypeBuilder<CardEntry> builder)
    {
        builder.ToTable("card_entries", t =>
        {
            t.HasCheckConstraint("ck_total_amount_nonneg", "total_amount >= 0");
            t.HasCheckConstraint("ck_installments_count_min1", "installments_count >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CardId).HasColumnName("card_id");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.PurchaseDate).HasColumnName("purchase_date");
        builder.Property(x => x.Label).HasColumnName("label").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.InstallmentsCount).HasColumnName("installments_count");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.CardEntries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Card)
            .WithMany(c => c.CardEntries)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Template)
            .WithMany(t => t.Instances)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.PurchaseDate }).HasDatabaseName("ix_card_entries_user_date");
        builder.HasIndex(x => x.CardId).HasDatabaseName("ix_card_entries_card");
        builder.HasIndex(x => x.TemplateId).HasDatabaseName("ix_card_entries_template");
    }
}
