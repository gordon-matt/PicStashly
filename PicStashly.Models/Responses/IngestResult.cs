namespace PicStashly.Models.Responses;

/// <summary>
/// Result of the ingest pipeline. When near-duplicates are found and the upload was not forced,
/// <see cref="Image"/> is null and <see cref="Duplicates"/> holds the matches so the client can
/// prompt the user ("view existing / save anyway / merge tags").
/// </summary>
public record IngestResult(bool IsDuplicate, ImageDetail? Image, List<DuplicateMatch> Duplicates)
{
    public static IngestResult Saved(ImageDetail image) => new(false, image, []);

    public static IngestResult DuplicateFound(List<DuplicateMatch> duplicates) => new(true, null, duplicates);
}