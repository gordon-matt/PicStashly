namespace PicStashly.Services.Abstractions;

public interface IImageService
{
    Task<Result<ImageSearchResponse>> SearchAsync(ImageSearchRequest request);

    Task<Result<ImageDetail>> GetByIdAsync(int id);

    /// <summary>
    /// Runs the full ingest pipeline: store + downscale, pHash + dedup check, OCR, persist, tag.
    /// When near-duplicates are found and <paramref name="force"/> is false, nothing is persisted
    /// and the result carries the duplicate matches for the client to prompt on.
    /// </summary>
    Task<Result<IngestResult>> IngestAsync(
        Stream stream,
        string fileName,
        string? sourceUrl,
        List<string>? tagNames,
        int? categoryId,
        bool force,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads the image from a URL (bookmarklet capture) and ingests it.</summary>
    Task<Result<IngestResult>> IngestFromUrlAsync(CaptureRequest request, bool force, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id);

    Task<Result<List<string>>> SetTagsAsync(int id, SetImageTagsRequest request);

    /// <summary>Reassigns (or clears) an image's category, moving its stored files into the new category's subfolder.</summary>
    Task<Result<ImageDetail>> SetCategoryAsync(int id, SetImageCategoryRequest request);

    Task<Result> UpdateOcrTextAsync(int id, UpdateOcrTextRequest request);

    Task<Result> BulkTagAsync(BulkTagRequest request);
}