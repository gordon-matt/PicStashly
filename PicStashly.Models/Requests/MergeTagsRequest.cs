namespace PicStashly.Models.Requests;

/// <summary>Merges all <paramref name="SourceTagIds"/> into <paramref name="TargetTagId"/> and deletes the sources.</summary>
public record MergeTagsRequest(int TargetTagId, List<int> SourceTagIds);
