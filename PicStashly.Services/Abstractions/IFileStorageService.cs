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
    /// Saves an image under <c>images/{ab}/{cd}/{guid}{ext}</c> (hashed subfolders so no single
    /// directory grows huge), downscaling to the configured maximum dimensions when needed, and
    /// generates a thumbnail under <c>thumbnails/{ab}/{cd}/{guid}.webp</c>.
    /// The original format is preserved; images that don't need downscaling are stored byte-for-byte.
    /// </summary>
    Task<StoredImageFile> SaveImageAsync(Stream sourceStream, string originalFileName, CancellationToken cancellationToken = default);

    /// <summary>Regenerates the thumbnail for an existing stored image. Returns the thumbnail's relative path.</summary>
    Task<string> RegenerateThumbnailAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Enumerates all stored image files (relative paths, excluding thumbnails). Used by the orphan check.</summary>
    IEnumerable<string> EnumerateImageFiles();
}
