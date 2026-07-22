namespace PicStashly.Services.Abstractions;

/// <summary>Metadata for a stored image file; paths are relative to the storage base path (forward slashes).</summary>
public record StoredImageFile(
    string StoragePath,
    string ThumbnailPath,
    string ContentType,
    long FileSizeBytes,
    int Width,
    int Height);

public interface IFileStorageService
{
    string GetBaseStoragePath();

    string GetFullPath(string relativePath);

    void DeleteFile(string relativePath);

    /// <summary>
    /// Derives the on-disk subfolder name for a category: a URL/filesystem-safe slug of its name
    /// (e.g. "Reaction Images" → "reaction-images"), falling back to "category-{id}" for names that
    /// slugify to nothing, or "uncategorized" when there is no category.
    /// </summary>
    string GetCategoryFolderName(int? categoryId, string? categoryName);

    /// <summary>
    /// Saves an image under <c>images/{categoryFolder}/{yyyyMMdd_HHmmss}_{n}{ext}</c> — human-readable,
    /// grouped by category, with <c>{n}</c> disambiguating multiple files added in the same second (e.g.
    /// a batch upload) — downscaling to the configured maximum dimensions when needed, and generates a
    /// thumbnail under the matching path in <c>thumbnails/{categoryFolder}/</c>.
    /// The original format is preserved; images that don't need downscaling are stored byte-for-byte.
    /// </summary>
    Task<StoredImageFile> SaveImageAsync(Stream sourceStream, string originalFileName, string categoryFolder, CancellationToken cancellationToken = default);

    /// <summary>Regenerates the thumbnail for an existing stored image. Returns the thumbnail's relative path.</summary>
    Task<string> RegenerateThumbnailAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an already-stored image (and its thumbnail) into a different category's subfolder,
    /// keeping the same file names. Returns the new relative paths.
    /// </summary>
    Task<(string StoragePath, string ThumbnailPath)> MoveToCategoryAsync(
        string currentStoragePath, string currentThumbnailPath, string newCategoryFolder, CancellationToken cancellationToken = default);

    /// <summary>Enumerates all stored image files (relative paths, excluding thumbnails). Used by the orphan check.</summary>
    IEnumerable<string> EnumerateImageFiles();
}