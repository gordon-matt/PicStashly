namespace PicStashly.Models.Responses;

public record ImageSearchResponse(
    List<ImageListItem> Images,
    int TotalCount,
    int TotalPages,
    int Page,
    int PageSize);
