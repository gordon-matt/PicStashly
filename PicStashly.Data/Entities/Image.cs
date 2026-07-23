using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PicStashly.Data.Entities;

public class Image : BaseEntity<int>
{
    /// <summary>Original file name as uploaded (used for search and download).</summary>
    public required string FileName { get; set; }

    /// <summary>Path of the stored file, relative to the storage base path (forward slashes).</summary>
    public required string StoragePath { get; set; }

    /// <summary>Path of the generated thumbnail, relative to the storage base path (forward slashes).</summary>
    public required string ThumbnailPath { get; set; }

    public required string ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>64-bit perceptual (difference) hash used for near-duplicate detection.</summary>
    public long PerceptualHash { get; set; }

    /// <summary>Text extracted from the image via OCR. Editable/correctable by the user.</summary>
    public string? OcrText { get; set; }

    /// <summary>Page URL when the image was captured via the bookmarklet; otherwise null.</summary>
    public string? SourceUrl { get; set; }

    public DateTime DateAdded { get; set; }

    /// <summary>
    /// Id of the owning user (the ASP.NET Core Identity user id, or the Keycloak "sub" claim
    /// when using Keycloak). Not modeled as a foreign key since Keycloak-authenticated users
    /// have no row in the local user table. Also determines the top-level storage subfolder.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>Optional category the image is filed under. Also determines its storage subfolder.</summary>
    public int? CategoryId { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ICollection<ImageTag> ImageTags { get; set; } = [];
}

public class ImageMap : IEntityTypeConfiguration<Image>
{
    public void Configure(EntityTypeBuilder<Image> builder)
    {
        builder.ToTable("Images", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FileName).IsRequired().HasMaxLength(512).IsUnicode(true);
        builder.Property(m => m.StoragePath).IsRequired().HasMaxLength(512);
        builder.Property(m => m.ThumbnailPath).IsRequired().HasMaxLength(512);
        builder.Property(m => m.ContentType).IsRequired().HasMaxLength(128);
        builder.Property(m => m.OcrText).IsUnicode(true);
        builder.Property(m => m.SourceUrl).HasMaxLength(2048);
        builder.Property(m => m.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(m => m.PerceptualHash);
        builder.HasIndex(m => new { m.UserId, m.DateAdded });
        builder.HasIndex(m => new { m.UserId, m.CategoryId });
        builder.HasOne(m => m.Category)
            .WithMany()
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}