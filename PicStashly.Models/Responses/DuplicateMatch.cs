namespace PicStashly.Models.Responses;

/// <summary>A near-duplicate found during ingest; <paramref name="Distance"/> is the pHash Hamming distance (0 = identical).</summary>
public record DuplicateMatch(
    int Id,
    string FileName,
    string Url,
    string ThumbnailUrl,
    int Distance,
    List<string> Tags);
