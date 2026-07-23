using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PicStashly.Data.Entities;

public class Category : BaseEntity<int>
{
    public required string Name { get; set; }

    /// <summary>
    /// Id of the owning user (the ASP.NET Core Identity user id, or the Keycloak "sub" claim
    /// when using Keycloak). Not modeled as a foreign key since Keycloak-authenticated users
    /// have no row in the local user table.
    /// </summary>
    public required string UserId { get; set; }
}

public class CategoryMap : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", "app");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(256).IsUnicode(true);
        builder.Property(c => c.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(c => new { c.UserId, c.Name }).IsUnique();
    }
}