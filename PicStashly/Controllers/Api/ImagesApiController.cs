namespace PicStashly.Controllers.Api;

[ApiController]
[Route("api/images")]
public class ImagesApiController : ControllerBase
{
    private readonly IImageService imageService;

    public ImagesApiController(IImageService imageService)
    {
        this.imageService = imageService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 60,
        [FromQuery] string? search = null,
        [FromQuery] string? tags = null,
        [FromQuery] bool untaggedOnly = false,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string sort = "newest",
        [FromQuery] int shuffleSeed = 0)
    {
        var result = await imageService.SearchAsync(new ImageSearchRequest(
            page, pageSize, search, tags, untaggedOnly, dateFrom, dateTo, sort, shuffleSeed));

        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await imageService.GetByIdAsync(id);
        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpPost("upload")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string? tags = null,
        [FromForm] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file was uploaded." });
        }

        var tagNames = string.IsNullOrWhiteSpace(tags)
            ? null
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        using var stream = file.OpenReadStream();
        var result = await imageService.IngestAsync(stream, file.FileName, sourceUrl: null, tagNames, force, cancellationToken);

        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await imageService.DeleteAsync(id);
        return result.ToActionResult(this, () => Ok(new { message = "Image deleted." }));
    }

    [HttpPut("{id:int}/tags")]
    public async Task<IActionResult> SetTags(int id, [FromBody] SetImageTagsRequest request)
    {
        var result = await imageService.SetTagsAsync(id, request);
        return result.ToActionResult(this, value => Ok(new { tags = value }));
    }

    [HttpPut("{id:int}/ocr")]
    public async Task<IActionResult> UpdateOcrText(int id, [FromBody] UpdateOcrTextRequest request)
    {
        var result = await imageService.UpdateOcrTextAsync(id, request);
        return result.ToActionResult(this, () => Ok(new { message = "Text updated." }));
    }

    [HttpPost("bulk-tags")]
    public async Task<IActionResult> BulkTag([FromBody] BulkTagRequest request)
    {
        var result = await imageService.BulkTagAsync(request);
        return result.ToActionResult(this, () => Ok(new { message = "Tags updated." }));
    }
}
