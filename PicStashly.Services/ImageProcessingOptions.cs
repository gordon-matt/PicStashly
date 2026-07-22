namespace PicStashly.Services;

public class ImageProcessingOptions
{
    /// <summary>When true, uploaded images larger than <see cref="MaxWidth"/>×<see cref="MaxHeight"/> are downscaled to fit.</summary>
    public bool AutoShrinkImages { get; set; } = true;

    public int MaxWidth { get; set; } = 1920;

    public int MaxHeight { get; set; } = 1920;

    /// <summary>JPEG/WebP encoder quality for the main image (only applied when re-encoding).</summary>
    public int Quality { get; set; } = 85;

    public int ThumbnailWidth { get; set; } = 400;

    public int ThumbnailHeight { get; set; } = 400;

    public int ThumbnailQuality { get; set; } = 75;
}