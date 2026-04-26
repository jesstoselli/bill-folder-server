using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Key).HasColumnName("key").IsRequired();
        b.Property(x => x.NamePt).HasColumnName("name_pt").IsRequired();
        b.Property(x => x.IsSystem).HasColumnName("is_system").HasDefaultValue(false);
        b.Property(x => x.DisplayOrder).HasColumnName("display_order");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        b.HasIndex(x => x.Key).IsUnique();
    }
}
