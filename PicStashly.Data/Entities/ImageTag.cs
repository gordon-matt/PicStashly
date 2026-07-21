using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PicStashly.Data.Entities;

public class ImageTag : IEntity
{
    public required int ImageId { get; set; }

    public required int TagId { get; set; }

    public virtual Image Image { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;

    [IgnoreDataMember]
    public object[] KeyValues => [ImageId, TagId];
}

public class ImageTagMap : IEntityTypeConfiguration<ImageTag>
{
    public void Configure(EntityTypeBuilder<ImageTag> builder)
    {
        builder.ToTable("ImageTags", "app");

        builder.HasKey(m => new { m.ImageId, m.TagId });
        builder.Property(m => m.ImageId).IsRequired();
        builder.Property(m => m.TagId).IsRequired();

        builder.HasOne(m => m.Image)
            .WithMany(i => i.ImageTags)
            .HasForeignKey(m => m.ImageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Tag)
            .WithMany(t => t.ImageTags)
            .HasForeignKey(m => m.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
