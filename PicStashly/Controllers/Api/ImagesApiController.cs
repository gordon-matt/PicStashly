namespace PicStashly.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/images")]
[TranslateResultToActionResult]
public class ImagesApiController : ControllerBase
{
    private readonly IImageService imageService;

    public ImagesApiController(IImageService imageService)
    {
        this.imageService = imageService;
    }

    [HttpGet("search")]
    public Task<Result<ImageSearchResponse>> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 60,
        [FromQuery] string? search = null,
        [FromQuery] string? tags = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool untaggedOnly = false,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string sort = "newest",
        [FromQuery] int shuffleSeed = 0) =>
        imageService.SearchAsync(new ImageSearchRequest(
            page, pageSize, search, tags, categoryId, untaggedOnly, dateFrom, dateTo, sort, shuffleSeed));

    [HttpGet("{id:int}")]
    public Task<Result<ImageDetail>> GetById(int id) => imageService.GetByIdAsync(id);

    [HttpPost("upload")]
    [RequestSizeLimit(524_288_000)]
    public async Task<Result<IngestResult>> Upload(
        IFormFile file,
        [FromForm] string? tags = null,
        [FromForm] int? categoryId = null,
        [FromForm] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return Result.Invalid([new ValidationError("File", "No file was uploaded.")]);
        }

        var tagNames = string.IsNullOrWhiteSpace(tags)
            ? null
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        using var stream = file.OpenReadStream();
        return await imageService.IngestAsync(stream, file.FileName, sourceUrl: null, tagNames, categoryId, force, cancellationToken);
    }

    [HttpDelete("{id:int}")]
    public Task<Result> Delete(int id) => imageService.DeleteAsync(id);

    [HttpPut("{id:int}/tags")]
    public Task<Result<List<string>>> SetTags(int id, [FromBody] SetImageTagsRequest request) =>
        imageService.SetTagsAsync(id, request);

    [HttpPut("{id:int}/category")]
    public Task<Result<ImageDetail>> SetCategory(int id, [FromBody] SetImageCategoryRequest request) =>
        imageService.SetCategoryAsync(id, request);

    [HttpPut("{id:int}/ocr")]
    public Task<Result> UpdateOcrText(int id, [FromBody] UpdateOcrTextRequest request) =>
        imageService.UpdateOcrTextAsync(id, request);

    [HttpPost("bulk-tags")]
    public Task<Result> BulkTag([FromBody] BulkTagRequest request) => imageService.BulkTagAsync(request);
}
