using System.Text;
using Heroicsoft.NetForge.Drawing;
using Microsoft.Extensions.Options;

namespace PicStashly.Services;

public class FileStorageService(
    IConfiguration configuration,
    IOptions<ImageProcessingOptions> imageOptions,
    IImageProcessor images) : IFileStorageService
{
    private const string UncategorizedFolder = "uncategorized";
    private const string ReservationExtension = ".reserved";

    // Guards the "pick a unique yyyyMMdd_HHmmss_{n} name for this category folder" critical section
    // so that concurrent uploads (e.g. a batch) never collide on the same second.
    private static readonly object namingLock = new();

    private string BasePath => configuration["FileStorage:BasePath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public string GetBaseStoragePath() => BasePath;

    public string GetFullPath(string relativePath) => Path.Combine(BasePath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public void DeleteFile(string relativePath)
    {
        string fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public string GetCategoryFolderName(int? categoryId, string? categoryName)
    {
        if (categoryId is null || string.IsNullOrWhiteSpace(categoryName))
        {
            return UncategorizedFolder;
        }

        string slug = Slugify(categoryName);
        return string.IsNullOrEmpty(slug) ? $"category-{categoryId}" : slug;
    }

    public async Task<StoredImageFile> SaveImageAsync(Stream sourceStream, string originalFileName, string categoryFolder, CancellationToken cancellationToken = default)
    {
        var opts = imageOptions.Value;

        // Buffer the stream so we can read it multiple times.
        using var buffer = new MemoryStream();
        await sourceStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        string extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        string imagesDir = GetFullPath($"{Constants.FileStorage.Images}/{categoryFolder}");
        Directory.CreateDirectory(imagesDir);

        string fileBaseName = ReserveFileBaseName(imagesDir);
        string reservationPath = Path.Combine(imagesDir, fileBaseName + ReservationExtension);

        try
        {
            string storageRelative = $"{Constants.FileStorage.Images}/{categoryFolder}/{fileBaseName}{extension}";
            string storageFull = GetFullPath(storageRelative);

            var info = await images.TryIdentifyAsync(buffer, cancellationToken);
            if (info is null)
            {
                throw new RasterDecodeException("The file is not a valid image.");
            }

            int width = info.Width;
            int height = info.Height;

            bool needsResize = opts.AutoShrinkImages && (width > opts.MaxWidth || height > opts.MaxHeight);
            ProcessedImage? resized = null;
            if (needsResize)
            {
                // Resize preserves the original format (and animation frames for GIF/WebP).
                resized = await images.ResizeAsync(buffer, new ImageTransform
                {
                    Width = opts.MaxWidth,
                    Height = opts.MaxHeight,
                    Mode = ImageScaleMode.Max,
                    Format = ImageFormats.FromExtension(extension),
                    Quality = opts.Quality,
                }, cancellationToken);

                width = resized.Width;
                height = resized.Height;
            }

            // Decode the thumbnail before writing the original so a bad file throws
            // RasterDecodeException without leaving an image behind.
            var thumbnail = resized is null
                ? await images.ResizeAsync(buffer, ThumbnailTransform(opts), cancellationToken)
                : await images.ResizeAsync(resized.Bytes, ThumbnailTransform(opts), cancellationToken);

            if (resized is not null)
            {
                await File.WriteAllBytesAsync(storageFull, resized.Bytes, cancellationToken);
            }
            else
            {
                // No processing needed — store the original bytes untouched.
                buffer.Position = 0;
                await using var fs = new FileStream(storageFull, FileMode.Create);
                await buffer.CopyToAsync(fs, cancellationToken);
            }

            string thumbnailRelative = await WriteThumbnailAsync(thumbnail.Bytes, fileBaseName, categoryFolder, cancellationToken);

            long fileSize = new FileInfo(storageFull).Length;

            return new StoredImageFile(
                storageRelative,
                thumbnailRelative,
                GetContentType(extension),
                fileSize,
                width,
                height);
        }
        finally
        {
            if (File.Exists(reservationPath))
            {
                File.Delete(reservationPath);
            }
        }
    }

    public async Task<string> RegenerateThumbnailAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        string fullPath = GetFullPath(storagePath);
        string fileBaseName = Path.GetFileNameWithoutExtension(storagePath);
        string categoryFolder = GetSubfolderFromPath(storagePath);

        var opts = imageOptions.Value;
        var thumbnail = await images.ResizeAsync(fullPath, ThumbnailTransform(opts), cancellationToken);
        return await WriteThumbnailAsync(thumbnail.Bytes, fileBaseName, categoryFolder, cancellationToken);
    }

    public Task<(string StoragePath, string ThumbnailPath)> MoveToCategoryAsync(
        string currentStoragePath, string currentThumbnailPath, string newCategoryFolder, CancellationToken cancellationToken = default)
    {
        string newStorageRelative = $"{Constants.FileStorage.Images}/{newCategoryFolder}/{Path.GetFileName(currentStoragePath)}";
        string newThumbnailRelative = $"{Constants.FileStorage.Thumbnails}/{newCategoryFolder}/{Path.GetFileName(currentThumbnailPath)}";

        MoveFileIfChanged(currentStoragePath, newStorageRelative);
        MoveFileIfChanged(currentThumbnailPath, newThumbnailRelative);

        return Task.FromResult((newStorageRelative, newThumbnailRelative));
    }

    public IEnumerable<string> EnumerateImageFiles()
    {
        string imagesRoot = GetFullPath(Constants.FileStorage.Images);
        if (!Directory.Exists(imagesRoot))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(imagesRoot, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(ReservationExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return Path.GetRelativePath(BasePath, file).Replace('\\', '/');
        }
    }

    private void MoveFileIfChanged(string currentRelativePath, string newRelativePath)
    {
        string currentFull = GetFullPath(currentRelativePath);
        string newFull = GetFullPath(newRelativePath);

        if (string.Equals(currentFull, newFull, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!File.Exists(currentFull))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(newFull)!);
        File.Move(currentFull, newFull, overwrite: true);
    }

    private static string ReserveFileBaseName(string imagesDir)
    {
        lock (namingLock)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            int n = 1;
            string candidate;
            while (true)
            {
                candidate = $"{timestamp}_{n}";
                if (Directory.GetFiles(imagesDir, $"{candidate}.*").Length == 0)
                {
                    break;
                }
                n++;
            }

            // Reserve the name immediately (before the slower decode/resize/save work happens) so a
            // concurrent call in the same second doesn't pick the same {n}.
            File.Create(Path.Combine(imagesDir, candidate + ReservationExtension)).Dispose();
            return candidate;
        }
    }

    private static string GetSubfolderFromPath(string relativePath)
    {
        // Expected shape: "images/{userId}/{categoryFolder}/{fileName}" — everything between the
        // top-level "images"/"thumbnails" segment and the file name is the caller-supplied folder
        // (userId, or userId + category slug), so this stays correct regardless of nesting depth.
        string[] parts = relativePath.Split('/');
        return parts.Length >= 3 ? string.Join('/', parts[1..^1]) : UncategorizedFolder;
    }

    private static string Slugify(string value)
    {
        var sb = new StringBuilder(value.Length);
        bool lastWasSeparator = true; // suppresses a leading '-'

        foreach (char c in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                sb.Append('-');
                lastWasSeparator = true;
            }
        }

        while (sb.Length > 0 && sb[^1] == '-')
        {
            sb.Length--;
        }

        return sb.ToString();
    }

    private static ImageTransform ThumbnailTransform(ImageProcessingOptions opts) => new()
    {
        Width = opts.ThumbnailWidth,
        Height = opts.ThumbnailHeight,
        Mode = ImageScaleMode.Max,
        Format = RasterFormat.Webp,
        Quality = opts.ThumbnailQuality,
    };

    private async Task<string> WriteThumbnailAsync(byte[] bytes, string fileBaseName, string categoryFolder, CancellationToken cancellationToken)
    {
        // WebP thumbnails: small, and unlike JPEG they keep transparency intact.
        string thumbnailRelative = $"{Constants.FileStorage.Thumbnails}/{categoryFolder}/{fileBaseName}.webp";
        string thumbnailFull = GetFullPath(thumbnailRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailFull)!);
        await File.WriteAllBytesAsync(thumbnailFull, bytes, cancellationToken);
        return thumbnailRelative;
    }

    public static string GetContentType(string extension) =>
        ImageFormats.ToContentType(ImageFormats.FromExtension(extension));
}