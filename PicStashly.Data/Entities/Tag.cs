using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PicStashly.Data.Entities;

public class Tag : BaseEntity<int>
{
    /// <summary>Tag name, normalized to lowercase for matching.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Id of the owning user (the ASP.NET Core Identity user id, or the Keycloak "sub" claim
    /// when using Keycloak). Not modeled as a foreign key since Keycloak-authenticated users
    /// have no row in the local user table.
    /// </summary>
    public required string UserId { get; set; }

    public virtual ICollection<ImageTag> ImageTags { get; set; } = [];
}

public class TagMap : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(128).IsUnicode(true);
        builder.Property(m => m.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(m => new { m.UserId, m.Name }).IsUnique();
    }
}