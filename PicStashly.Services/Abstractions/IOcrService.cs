namespace PicStashly.Services.Abstractions;

public interface IOcrService
{
    /// <summary>
    /// Extracts text from the image at the given path. Returns null when OCR is disabled,
    /// tesseract is unavailable, or no text was found. Never throws — OCR is best-effort.
    /// </summary>
    Task<string?> ExtractTextAsync(string fullPath, CancellationToken cancellationToken = default);
}
