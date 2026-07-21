namespace PicStashly.Services;

public class UploadOptions
{
    public string AllowedExtensions { get; set; } = ".jpg,.jpeg,.png,.gif,.webp,.bmp";

    public long MaxFileSizeBytes { get; set; } = 52_428_800; // 50 MB

    /// <summary>Maximum pHash Hamming distance (0-64) for two images to be considered near-duplicates.</summary>
    public int DuplicateHammingThreshold { get; set; } = 5;

    public HashSet<string> AllowedExtensionSet
        => field ??= AllowedExtensions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
