namespace PicStashly.Models.Requests;

/// <summary>Bookmarklet capture: the server downloads <paramref name="ImageUrl"/> and ingests it.</summary>
public record CaptureRequest(string ImageUrl, string? PageUrl = null, List<string>? TagNames = null, int? CategoryId = null);
