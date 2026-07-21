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
    private string BasePath => configuration["FileStorage:BasePath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public string GetBaseStoragePath() => BasePath;

    public string GetFullPath(string relativePath)
        => Path.Combine(BasePath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public void DeleteFile(string relativePath)
    {
        string fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public async Task<StoredImageFile> SaveImageAsync(Stream sourceStream, string originalFileName, CancellationToken cancellationToken = default)
    {
        var opts = imageOptions.Value;

        // Buffer the stream so we can read it multiple times.
        using var buffer = new MemoryStream();
        await sourceStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        string extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        string name = Guid.NewGuid().ToString("N");

        // Hashed subfolder structure (e.g. images/ab/cd/abcd....png) so no single directory grows huge.
        string subfolder = $"{name[..2]}/{name[2..4]}";
        string storageRelative = $"{Constants.FileStorage.Images}/{subfolder}/{name}{extension}";
        string storageFull = GetFullPath(storageRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(storageFull)!);

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

        string thumbnailRelative = await GenerateThumbnailAsync(image, name, subfolder, cancellationToken);

        long fileSize = new FileInfo(storageFull).Length;

        return new StoredImageFile(
            storageRelative,
            thumbnailRelative,
            GetContentType(extension),
            fileSize,
            width,
            height);
    }

    public async Task<string> RegenerateThumbnailAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        string fullPath = GetFullPath(storagePath);
        string name = Path.GetFileNameWithoutExtension(storagePath);
        string subfolder = $"{name[..2]}/{name[2..4]}";

        using var image = await ImageSharpImage.LoadAsync(fullPath, cancellationToken);
        return await GenerateThumbnailAsync(image, name, subfolder, cancellationToken);
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
            yield return Path.GetRelativePath(BasePath, file).Replace('\\', '/');
        }
    }

    private async Task<string> GenerateThumbnailAsync(ImageSharpImage image, string name, string subfolder, CancellationToken cancellationToken)
    {
        var opts = imageOptions.Value;

        // WebP thumbnails: small, and unlike JPEG they keep transparency intact.
        string thumbnailRelative = $"{Constants.FileStorage.Thumbnails}/{subfolder}/{name}.webp";
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
