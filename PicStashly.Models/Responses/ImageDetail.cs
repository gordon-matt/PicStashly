namespace PicStashly.Models.Responses;

public record ImageDetail(
    int Id,
    string FileName,
    string Url,
    string ThumbnailUrl,
    string ContentType,
    long FileSizeBytes,
    int Width,
    int Height,
    string PerceptualHash,
    string? OcrText,
    string? SourceUrl,
    DateTime DateAdded,
    List<string> Tags);
