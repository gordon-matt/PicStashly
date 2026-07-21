using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PicStashly.Data.Entities;

public class Tag : BaseEntity<int>
{
    /// <summary>Tag name, normalized to lowercase for matching.</summary>
    public required string Name { get; set; }

    public virtual ICollection<ImageTag> ImageTags { get; set; } = [];
}

public class TagMap : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(128).IsUnicode(true);
        builder.HasIndex(m => m.Name).IsUnique();
    }
}
