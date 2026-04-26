using BillFolder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFolder.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Key).HasColumnName("key").IsRequired();
        builder.Property(x => x.NamePt).HasColumnName("name_pt").IsRequired();
        builder.Property(x => x.IsSystem).HasColumnName("is_system").HasDefaultValue(false);
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.HasIndex(x => x.Key).IsUnique();
    }
}
