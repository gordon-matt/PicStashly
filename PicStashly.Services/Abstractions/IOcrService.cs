namespace PicStashly.Services.Abstractions;

public interface IOcrService
{
    /// <summary>
    /// Extracts text from the image at the given path. Returns null when OCR is disabled,
    /// the OCR engine is unavailable, or no text was found. Never throws — OCR is best-effort.
    /// </summary>
    Task<string?> ExtractTextAsync(string fullPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether OCR is enabled AND the PaddleOCR native stack loaded successfully. Surfaced on the
    /// Admin page so a broken native setup doesn't just silently produce empty OCR text forever.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}