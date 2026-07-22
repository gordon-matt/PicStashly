using System.Text;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace PicStashly.Services;

public class FileStorageService(IConfiguration configuration, IOptions<ImageProcessingOptions> imageOptions) : IFileStorageService
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

            using var image = await ImageSharpImage.LoadAsync(buffer, cancellationToken);
            int width = image.Width;
            int height = image.Height;

            bool needsResize = opts.AutoShrinkImages && (width > opts.MaxWidth || height > opts.MaxHeight);
            if (needsResize)
            {
                // Resize preserves the original format (and animation frames for GIF/WebP).
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(opts.MaxWidth, opts.MaxHeight)
                }));

                width = image.Width;
                height = image.Height;

                await image.SaveAsync(storageFull, GetEncoder(extension, opts.Quality), cancellationToken);
            }
            else
            {
                // No processing needed — store the original bytes untouched.
                buffer.Position = 0;
                await using var fs = new FileStream(storageFull, FileMode.Create);
                await buffer.CopyToAsync(fs, cancellationToken);
            }

            string thumbnailRelative = await GenerateThumbnailAsync(image, fileBaseName, categoryFolder, cancellationToken);

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
        string categoryFolder = GetCategoryFolderFromPath(storagePath);

        using var image = await ImageSharpImage.LoadAsync(fullPath, cancellationToken);
        return await GenerateThumbnailAsync(image, fileBaseName, categoryFolder, cancellationToken);
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

    private static string GetCategoryFolderFromPath(string relativePath)
    {
        // Expected shape: "images/{categoryFolder}/{fileName}"
        string[] parts = relativePath.Split('/');
        return parts.Length >= 3 ? parts[1] : UncategorizedFolder;
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

    private async Task<string> GenerateThumbnailAsync(ImageSharpImage image, string fileBaseName, string categoryFolder, CancellationToken cancellationToken)
    {
        var opts = imageOptions.Value;

        // WebP thumbnails: small, and unlike JPEG they keep transparency intact.
        string thumbnailRelative = $"{Constants.FileStorage.Thumbnails}/{categoryFolder}/{fileBaseName}.webp";
        string thumbnailFull = GetFullPath(thumbnailRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailFull)!);

        using var thumbnail = image.Clone(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(opts.ThumbnailWidth, opts.ThumbnailHeight)
        }));

        await thumbnail.SaveAsync(thumbnailFull, new WebpEncoder { Quality = opts.ThumbnailQuality }, cancellationToken);
        return thumbnailRelative;
    }

    private static ImageEncoder GetEncoder(string extension, int quality) => extension switch
    {
        ".jpg" or ".jpeg" => new JpegEncoder { Quality = quality },
        ".png" => new PngEncoder(),
        ".gif" => new GifEncoder(),
        ".webp" => new WebpEncoder { Quality = quality },
        ".bmp" => new BmpEncoder(),
        _ => new JpegEncoder { Quality = quality }
    };

    public static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "application/octet-stream"
    };
}