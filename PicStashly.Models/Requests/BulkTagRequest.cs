namespace PicStashly.Models.Requests;

/// <summary>Applies or removes a set of tags on multiple images in one action.</summary>
public record BulkTagRequest(List<int> ImageIds, List<string> TagNames, bool Remove = false);