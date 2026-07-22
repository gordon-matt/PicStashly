namespace PicStashly.Models.Responses;

/// <summary>
/// <paramref name="OrphanedFiles"/>: files on disk with no matching DB record.
/// <paramref name="BrokenImages"/>: DB records whose file is missing on disk.
/// </summary>
public record OrphanCheckResult(List<string> OrphanedFiles, List<ImageListItem> BrokenImages);