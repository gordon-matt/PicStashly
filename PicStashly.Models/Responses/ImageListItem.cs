namespace PicStashly.Models.Responses;

public record ImageListItem(
    int Id,
    string FileName,
    string Url,
    string ThumbnailUrl,
    int Width,
    int Height,
    DateTime DateAdded,
    List<string> Tags,
    int? CategoryId,
    string? CategoryName);
