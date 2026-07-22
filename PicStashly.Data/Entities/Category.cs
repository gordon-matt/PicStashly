using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PicStashly.Data.Entities;

public class Category : BaseEntity<int>
{
    public required string Name { get; set; }
}

public class CategoryMap : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", "app");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(256).IsUnicode(true);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}