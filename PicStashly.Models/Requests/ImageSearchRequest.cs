namespace PicStashly.Models.Requests;

public record ImageSearchRequest(
    int Page = 1,
    int PageSize = 60,
    string? Search = null,
    string? Tags = null,
    bool UntaggedOnly = false,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string Sort = "newest",
    int ShuffleSeed = 0);
